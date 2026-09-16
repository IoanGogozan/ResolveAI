import { screen } from '@testing-library/react';
import { HttpResponse, delay, http } from 'msw';
import { describe, expect, it } from 'vitest';
import type { AuditFact } from '@/api/types';
import { bodyless, caller } from '@/test/backend';
import { renderWithBackend } from '@/test/render';
import { server } from '@/test/server';
import { AuditTimeline } from './AuditTimeline';

const caseId = '0199a0a0-0000-7000-8000-000000000001';

const fact: AuditFact = {
  eventId: '0199a0a0-0000-7000-8000-0000000000ff',
  tenantId: '11111111-1111-1111-1111-111111111111',
  caseId,
  actorId: 'alpha-operator',
  actorType: 'Operator',
  action: 'case.created',
  occurredAt: '2026-09-16T10:00:00+00:00',
  traceParent: '00-1234567890abcdef1234567890abcdef-1234567890abcdef-01',
};

describe('audit timeline', () => {
  it('distinguishes pending delivery from an absence of events', async () => {
    // Audit is committed with the change and published by a worker, so "empty" is not "lost".
    renderWithBackend(<AuditTimeline caseId={caseId} />);
    expect(await screen.findByText(/No events delivered yet/)).toBeInTheDocument();
    expect(screen.getByText(/published by a background worker/)).toBeInTheDocument();
  });

  it('shows the actor and trace parent for each delivered fact', async () => {
    server.use(http.get('/api/cases/:id/audit', () => HttpResponse.json([fact])));
    renderWithBackend(<AuditTimeline caseId={caseId} />);

    expect(await screen.findByText('case.created')).toBeInTheDocument();
    expect(screen.getByText(/alpha-operator \(Operator\)/)).toBeInTheDocument();
    expect(screen.getByTitle(fact.traceParent!)).toBeInTheDocument();
  });

  it('does not report a refusal while the caller context is still loading', async () => {
    // Switching identity clears the cache, so there is a window with no permissions known yet.
    server.use(
      http.get('/api/me', async () => {
        await delay(120);
        return HttpResponse.json(caller());
      }),
    );
    renderWithBackend(<AuditTimeline caseId={caseId} />);

    expect(screen.getByText(/Loading the caller context/)).toBeInTheDocument();
    expect(screen.queryByText(/Reading audit requires/)).not.toBeInTheDocument();
    expect(await screen.findByText(/No events delivered yet/)).toBeInTheDocument();
  });

  it('does not request audit for an identity that lacks the stricter permission', async () => {
    server.use(
      http.get('/api/me', () => HttpResponse.json(caller({ permissions: ['cases.read'] }))),
      http.get('/api/cases/:id/audit', () => bodyless(403)),
    );
    renderWithBackend(<AuditTimeline caseId={caseId} />);

    expect(await screen.findByText(/Reading audit requires/)).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});
