import { ApiError } from '@/api/ApiError';

/**
 * Renders a backend refusal as the deliberate outcome it is. These explanations are the product
 * demonstration: enforcement, tenant isolation and uncertain results must be readable, not hidden
 * behind a generic failure message.
 */
const explanations: Record<string, string> = {
  Unauthenticated: 'The backend did not recognize this identity. Select one of the fixture identities.',
  AccessDenied: 'This identity lacks a permission the endpoint requires. The backend refused the request.',
  NotFound:
    'Not found for this identity. A case belonging to another tenant is deliberately indistinguishable from one that does not exist.',
  ValidationFailed: 'The request did not satisfy the backend validation rules.',
  InvalidProposalRequest: 'The proposal request was rejected before any provider call.',
  InvocationBudgetExceeded:
    'The estimated cost of this invocation exceeded the configured per-invocation limit, so no provider call was made.',
  InvalidReference: 'The business reference set is incomplete or malformed.',
  MissingReferences:
    'This case was created without business references, so there are no customer, order or payment facts to resolve.',
  AssociationMismatch:
    'The customer, order and payment do not belong together, or their currencies disagree. The backend refuses to treat them as one case.',
  OperationConflict:
    'This operation identifier was already used with different input. A materially different request needs a new operation.',
  Conflict: 'The request conflicts with the stored state.',
  Unavailable: 'The underlying data source did not answer in time. The request can be retried.',
  BackendUnreachable: 'The backend is not reachable. Check that the host is running on port 5080.',
  InternalError: 'The backend reported an unexpected failure. Use the trace identifier to investigate.',
};

export function Outcome({
  error,
  requiredPermission,
  onRetry,
}: {
  error: unknown;
  requiredPermission?: string;
  onRetry?: () => void;
}) {
  const problem =
    error instanceof ApiError
      ? error
      : new ApiError(0, 'UnexpectedClientError', error instanceof Error ? error.message : String(error), null);
  const explanation = explanations[problem.code] ?? problem.title;
  const retryable = problem.code === 'Unavailable' || problem.code === 'BackendUnreachable';

  return (
    <div className="outcome" role="alert">
      <div className="outcome__header">
        <span className="outcome__code">{problem.code}</span>
        {problem.status > 0 ? <span className="outcome__status">HTTP {problem.status}</span> : null}
      </div>
      <p className="outcome__explanation">{explanation}</p>
      {problem.code === 'AccessDenied' && requiredPermission ? (
        <p className="outcome__detail">
          Required permission: <code>{requiredPermission}</code>
        </p>
      ) : null}
      {problem.title !== explanation ? <p className="outcome__detail">Backend said: {problem.title}</p> : null}
      {problem.traceId ? (
        <p className="outcome__trace">
          Trace <code>{problem.traceId}</code>
        </p>
      ) : null}
      {onRetry && retryable ? (
        <button type="button" className="button button--quiet" onClick={onRetry}>
          Retry
        </button>
      ) : null}
    </div>
  );
}
