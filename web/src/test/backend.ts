import { HttpResponse, http } from 'msw';
import type { AuditFact, CallerContext, CasePage, CaseSummary, ProposalRun } from '@/api/types';

/**
 * Fixtures shaped like the real host's responses, including the ProblemDetails contract. Handlers are
 * typed against the same interfaces the application uses, so a contract change breaks these tests
 * rather than the console.
 */

export const alphaPermissions = [
  'audit.read',
  'cases.create',
  'cases.read',
  'customers.read',
  'orders.read',
  'payments.read',
  'proposals.create',
  'proposals.read',
];

export function caller(overrides: Partial<CallerContext> = {}): CallerContext {
  return {
    tenantId: '11111111-1111-1111-1111-111111111111',
    actorId: 'alpha-operator',
    actorType: 'Operator',
    permissions: alphaPermissions,
    ...overrides,
  };
}

export function supportCase(overrides: Partial<CaseSummary> = {}): CaseSummary {
  return {
    id: '0199a0a0-0000-7000-8000-000000000001',
    subject: 'Damaged monitor',
    description: 'The monitor arrived broken.',
    status: 'Created',
    createdAt: '2026-09-16T10:00:00+00:00',
    references: { customerId: 'customer-monitor', orderId: 'order-monitor', paymentId: 'payment-monitor' },
    ...overrides,
  };
}

export function proposalRun(overrides: Partial<ProposalRun> = {}): ProposalRun {
  return {
    runId: '0199a0a0-0000-7000-8000-00000000000a',
    operationId: 'b0a1c2d3-0000-4000-8000-00000000000a',
    caseId: supportCase().id,
    status: 'Completed',
    proposal: {
      action: 'RefundPayment',
      paymentId: 'payment-monitor',
      amount: 649,
      currency: 'EUR',
      reasonCode: 'DamagedItem',
      confidence: 0.94,
      evidence: ['case text', 'order', 'payment'],
    },
    errorCode: null,
    provider: 'Fixture',
    model: 'fixture-v1',
    promptVersion: 'refund-proposal-v1',
    schemaVersion: 'proposal-v1',
    inputTokens: 100,
    outputTokens: 50,
    estimatedCostUsd: 0.00012,
    latencyMs: 820,
    createdAt: '2026-09-16T10:01:00+00:00',
    completedAt: '2026-09-16T10:01:01+00:00',
    ...overrides,
  };
}

export function problem(status: number, code: string, title: string) {
  return HttpResponse.json({ status, title, code, traceId: '00-trace-0000000000000001-01' }, { status });
}

/** ASP.NET authorization and routing reject before the exception handler, producing no body at all. */
export function bodyless(status: number) {
  return new HttpResponse(null, { status });
}

export const defaultHandlers = [
  http.get('/api/me', () => HttpResponse.json(caller())),
  http.get('/api/cases', () => HttpResponse.json<CasePage>({ items: [supportCase()], nextCursor: null })),
  http.get('/api/cases/:id', () => HttpResponse.json(supportCase())),
  http.get('/api/cases/:id/business-facts', () =>
    HttpResponse.json({
      customer: { id: 'customer-monitor', displayName: 'Alpha Customer' },
      order: {
        id: 'order-monitor',
        customerId: 'customer-monitor',
        product: 'Monitor',
        total: 649,
        currency: 'EUR',
        deliveryStatus: 'Delivered',
      },
      payment: {
        id: 'payment-monitor',
        orderId: 'order-monitor',
        capturedAmount: 649,
        refundedAmount: 0,
        currency: 'EUR',
        status: 'Captured',
      },
    }),
  ),
  http.get('/api/cases/:id/proposals', () => HttpResponse.json<ProposalRun[]>([])),
  http.get('/api/cases/:id/audit', () => HttpResponse.json<AuditFact[]>([])),
  http.get('/health/:probe', () => HttpResponse.json({ status: 'ok' })),
];
