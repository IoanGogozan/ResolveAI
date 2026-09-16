// Mirrors the public module contracts of the backend. `status` fields are plain strings on purpose:
// Orchestration will add workflow states later, and an unknown state must render rather than crash.

export interface CaseBusinessReferences {
  customerId: string;
  orderId: string;
  paymentId: string;
}

export interface CaseSummary {
  id: string;
  subject: string;
  description: string;
  status: string;
  createdAt: string;
  references: CaseBusinessReferences | null;
}

export interface CasePage {
  items: CaseSummary[];
  nextCursor: string | null;
}

export interface CreateCaseRequest {
  subject: string;
  description: string;
  references?: CaseBusinessReferences;
}

export interface CustomerFacts {
  id: string;
  displayName: string;
}

export interface OrderFacts {
  id: string;
  customerId: string;
  product: string;
  total: number;
  currency: string;
  deliveryStatus: string;
}

export interface PaymentFacts {
  id: string;
  orderId: string;
  capturedAmount: number;
  refundedAmount: number;
  currency: string;
  status: string;
}

export interface CaseBusinessFacts {
  customer: CustomerFacts;
  order: OrderFacts;
  payment: PaymentFacts;
}

export interface StructuredProposal {
  action: string;
  paymentId: string | null;
  amount: number | null;
  currency: string | null;
  reasonCode: string;
  confidence: number;
  evidence: string[];
}

export interface ProposalRun {
  runId: string;
  operationId: string;
  caseId: string;
  status: string;
  proposal: StructuredProposal | null;
  errorCode: string | null;
  provider: string;
  model: string;
  promptVersion: string;
  schemaVersion: string;
  inputTokens: number | null;
  outputTokens: number | null;
  estimatedCostUsd: number | null;
  latencyMs: number | null;
  createdAt: string;
  completedAt: string | null;
}

export interface AuditFact {
  eventId: string;
  tenantId: string;
  caseId: string;
  actorId: string;
  actorType: string;
  action: string;
  occurredAt: string;
  traceParent: string | null;
}

export interface CallerContext {
  tenantId: string;
  actorId: string;
  actorType: string;
  permissions: string[];
}

export interface HealthStatus {
  status: string;
}

export const terminalRunStatuses = ['Completed', 'Failed', 'Interrupted'];

export function isRunFinished(run: ProposalRun): boolean {
  return terminalRunStatuses.includes(run.status);
}
