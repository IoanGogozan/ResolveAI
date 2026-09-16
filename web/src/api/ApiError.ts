/**
 * A failed backend call, normalized to the stable `code` the host emits in its ProblemDetails
 * extensions. Authorization and routing rejections are produced by ASP.NET before the exception
 * handler runs and therefore have no body, so those statuses get a synthesized code instead.
 */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly code: string,
    readonly title: string,
    readonly traceId: string | null,
  ) {
    super(`${status} ${code}: ${title}`);
    this.name = 'ApiError';
  }
}

const bodylessCodes: Record<number, string> = {
  400: 'ValidationFailed',
  401: 'Unauthenticated',
  403: 'AccessDenied',
  404: 'NotFound',
  405: 'RequestFailed',
  409: 'Conflict',
  500: 'InternalError',
  503: 'Unavailable',
};

const defaultTitles: Record<number, string> = {
  400: 'The request was rejected as invalid.',
  401: 'The backend did not recognize this identity.',
  403: 'This identity is not permitted to perform the operation.',
  404: 'Not found for this identity.',
  409: 'The request conflicts with the stored state.',
  500: 'An unexpected error occurred.',
  503: 'A dependency is unavailable.',
};

interface ProblemDetails {
  title?: string;
  detail?: string;
  code?: string | null;
  traceId?: string | null;
}

export async function toApiError(response: Response): Promise<ApiError> {
  const code = bodylessCodes[response.status] ?? 'RequestFailed';
  const title = defaultTitles[response.status] ?? `The backend returned ${response.status}.`;
  let body = '';
  try {
    body = await response.text();
  } catch {
    return new ApiError(response.status, code, title, null);
  }
  if (body.length === 0) return new ApiError(response.status, code, title, null);
  try {
    const problem = JSON.parse(body) as ProblemDetails;
    return new ApiError(
      response.status,
      problem.code ?? code,
      problem.title ?? problem.detail ?? title,
      problem.traceId ?? null,
    );
  } catch {
    return new ApiError(response.status, code, title, null);
  }
}

export function networkError(): ApiError {
  return new ApiError(0, 'BackendUnreachable', 'The backend could not be reached.', null);
}
