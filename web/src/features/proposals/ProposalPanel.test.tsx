import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { describe, expect, it, vi } from 'vitest';
import type { ProposalRun } from '@/api/types';
import { caller, proposalRun } from '@/test/backend';
import { renderWithBackend } from '@/test/render';
import { server } from '@/test/server';
import { ProposalPanel } from './ProposalPanel';

const caseId = '0199a0a0-0000-7000-8000-000000000001';

interface Recorder {
  operationIds: string[];
}

/** Records every POST so the tests can prove no invocation is repeated behind the operator's back. */
function backendWith(runs: ProposalRun[], respond: (operationId: string) => Response | Promise<Response>): Recorder {
  const recorder: Recorder = { operationIds: [] };
  server.use(
    http.post('/api/cases/:id/proposals', async ({ request }) => {
      const body = (await request.json()) as { operationId: string };
      recorder.operationIds.push(body.operationId);
      return respond(body.operationId);
    }),
    http.get('/api/cases/:id/proposals', () => HttpResponse.json(runs)),
  );
  return recorder;
}

describe('proposal panel', () => {
  it('polls a running invocation until the persisted result appears', async () => {
    const runs: ProposalRun[] = [];
    const recorder = backendWith(runs, (operationId) => {
      const started = proposalRun({
        operationId,
        status: 'Running',
        proposal: null,
        completedAt: null,
        createdAt: new Date().toISOString(),
      });
      runs.push(started);
      return HttpResponse.json(started, { status: 202 });
    });
    renderWithBackend(<ProposalPanel caseId={caseId} />);

    await userEvent.click(await screen.findByRole('button', { name: 'Generate proposal' }));
    expect(await screen.findByText('Running')).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('Polling for the persisted result');

    const first = runs[0]!;
    runs[0] = { ...first, status: 'Completed', proposal: proposalRun().proposal, completedAt: new Date().toISOString() };

    await waitFor(() => expect(screen.getByText('Completed')).toBeInTheDocument(), { timeout: 5000 });
    expect(screen.getByText('RefundPayment')).toBeInTheDocument();
    expect(screen.getByText('€649.00')).toBeInTheDocument();
    expect(recorder.operationIds).toHaveLength(1);
  });

  it('never reissues an uncertain invocation on its own', async () => {
    const runs: ProposalRun[] = [];
    const recorder = backendWith(runs, (operationId) => {
      const interrupted = proposalRun({
        operationId,
        status: 'Interrupted',
        errorCode: 'OutcomeUnknown',
        proposal: null,
      });
      runs.push(interrupted);
      return HttpResponse.json(interrupted);
    });
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
    renderWithBackend(<ProposalPanel caseId={caseId} />);

    await userEvent.click(await screen.findByRole('button', { name: 'Generate proposal' }));
    expect(await screen.findByText('Interrupted')).toBeInTheDocument();
    expect(screen.getByText(/records uncertainty instead of silently reissuing/i)).toBeInTheDocument();

    // A new run costs money, so it needs an explicit confirmation that says so.
    await userEvent.click(screen.getByRole('button', { name: 'Generate again' }));
    expect(confirm).toHaveBeenCalledWith(expect.stringContaining('may incur a new charge'));
    expect(recorder.operationIds).toHaveLength(1);
    confirm.mockRestore();
  });

  it('replays the same operation when the result was never received', async () => {
    const runs: ProposalRun[] = [];
    let attempt = 0;
    const recorder = backendWith(runs, (operationId) => {
      attempt += 1;
      if (attempt === 1) return HttpResponse.error();
      const completed = proposalRun({ operationId });
      runs.push(completed);
      return HttpResponse.json(completed);
    });
    renderWithBackend(<ProposalPanel caseId={caseId} />);

    await userEvent.click(await screen.findByRole('button', { name: 'Generate proposal' }));
    expect(await screen.findByText(/its result never arrived/i)).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Replay this operation' }));
    await waitFor(() => expect(screen.getByText('Completed')).toBeInTheDocument(), { timeout: 5000 });
    expect(recorder.operationIds).toHaveLength(2);
    expect(recorder.operationIds[0]).toBe(recorder.operationIds[1]);
  });

  it('offers no generation control to an identity without the permission', async () => {
    server.use(http.get('/api/me', () => HttpResponse.json(caller({ permissions: ['cases.read'] }))));
    renderWithBackend(<ProposalPanel caseId={caseId} />);

    expect(await screen.findByText(/Generating requires/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Generate proposal' })).not.toBeInTheDocument();
    expect(screen.getByText(/Reading proposals requires/)).toBeInTheDocument();
  });
});
