import { screen } from '@testing-library/react';
import { HttpResponse, http } from 'msw';
import { describe, expect, it } from 'vitest';
import { bodyless, caller, supportCase } from '@/test/backend';
import { renderWithBackend } from '@/test/render';
import { server } from '@/test/server';
import { CaseListPage } from './CaseListPage';

describe('case list', () => {
  it('lists the cases the backend returned for this identity', async () => {
    server.use(
      http.get('/api/cases', () =>
        HttpResponse.json({
          items: [supportCase(), supportCase({ id: 'second', subject: 'Duplicate charge', references: null })],
          nextCursor: 'second',
        }),
      ),
    );
    renderWithBackend(<CaseListPage />);

    expect(await screen.findByRole('link', { name: 'Damaged monitor' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Duplicate charge' })).toBeInTheDocument();
    expect(screen.getByText('payment-monitor')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Next' })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Previous' })).toBeDisabled();
  });

  it('names the missing permission instead of offering a control that would be refused', async () => {
    server.use(http.get('/api/me', () => HttpResponse.json(caller({ permissions: ['cases.read'] }))));
    renderWithBackend(<CaseListPage />);

    expect(await screen.findByText(/Creating cases requires/)).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'New case' })).not.toBeInTheDocument();
  });

  it('explains a bodyless authorization refusal as a deliberate outcome', async () => {
    server.use(http.get('/api/cases', () => bodyless(403)));
    renderWithBackend(<CaseListPage />);

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('AccessDenied');
    expect(alert).toHaveTextContent('cases.read');
  });

  it('reports a missing case without asserting that it exists elsewhere', async () => {
    server.use(http.get('/api/cases', () => bodyless(404)));
    renderWithBackend(<CaseListPage />);

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Not found for this identity');
    // The backend deliberately does not disclose existence across tenants, so neither may the console.
    expect(alert.textContent).not.toMatch(/exists in another tenant|belongs to tenant/i);
  });
});
