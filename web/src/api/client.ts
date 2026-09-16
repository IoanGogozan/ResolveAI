import { networkError, toApiError } from './ApiError';
import type {
  AuditFact,
  CallerContext,
  CaseBusinessFacts,
  CasePage,
  CaseSummary,
  CreateCaseRequest,
  HealthStatus,
  ProposalRun,
} from './types';

/** Applies credentials to an outgoing request. Only src/auth knows what those credentials are. */
export type Authorize = (headers: Headers) => void;

export interface ResolveAiApi {
  caller(): Promise<CallerContext>;
  listCases(options?: { limit?: number; cursor?: string | null }): Promise<CasePage>;
  getCase(id: string): Promise<CaseSummary>;
  createCase(request: CreateCaseRequest): Promise<CaseSummary>;
  businessFacts(caseId: string): Promise<CaseBusinessFacts>;
  listProposals(caseId: string): Promise<ProposalRun[]>;
  /**
   * Starts or replays a run. The host answers 202 when an equivalent run is already in flight and
   * 200 once it is terminal; both carry the run, so the caller only inspects `status`.
   */
  generateProposal(caseId: string, operationId: string): Promise<ProposalRun>;
  audit(caseId: string): Promise<AuditFact[]>;
  health(probe: 'live' | 'ready'): Promise<HealthStatus>;
}

export function createApiClient(authorize: Authorize): ResolveAiApi {
  async function send<T>(path: string, init?: RequestInit): Promise<T> {
    const headers = new Headers(init?.headers);
    headers.set('Accept', 'application/json');
    if (init?.body !== undefined) headers.set('Content-Type', 'application/json');
    authorize(headers);
    let response: Response;
    try {
      response = await fetch(path, { ...init, headers });
    } catch {
      throw networkError();
    }
    if (!response.ok) throw await toApiError(response);
    if (response.status === 204) return undefined as T;
    return (await response.json()) as T;
  }

  return {
    caller: () => send<CallerContext>('/api/me'),
    listCases: (options) => {
      const query = new URLSearchParams();
      if (options?.limit !== undefined) query.set('limit', String(options.limit));
      if (options?.cursor) query.set('cursor', options.cursor);
      const suffix = query.size > 0 ? `?${query.toString()}` : '';
      return send<CasePage>(`/api/cases${suffix}`);
    },
    getCase: (id) => send<CaseSummary>(`/api/cases/${id}`),
    createCase: (request) => send<CaseSummary>('/api/cases', { method: 'POST', body: JSON.stringify(request) }),
    businessFacts: (caseId) => send<CaseBusinessFacts>(`/api/cases/${caseId}/business-facts`),
    listProposals: (caseId) => send<ProposalRun[]>(`/api/cases/${caseId}/proposals`),
    generateProposal: (caseId, operationId) =>
      send<ProposalRun>(`/api/cases/${caseId}/proposals`, { method: 'POST', body: JSON.stringify({ operationId }) }),
    audit: (caseId) => send<AuditFact[]>(`/api/cases/${caseId}/audit`),
    health: (probe) => send<HealthStatus>(`/health/${probe}`),
  };
}
