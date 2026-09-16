import { describe, expect, it } from 'vitest';
import { toApiError } from './ApiError';

describe('backend failure normalization', () => {
  it('uses the stable code from the host problem response', async () => {
    const response = new Response(
      JSON.stringify({
        status: 422,
        title: 'Business references do not belong together.',
        code: 'AssociationMismatch',
        traceId: '00-abc-01',
      }),
      { status: 422, headers: { 'Content-Type': 'application/problem+json' } },
    );
    const error = await toApiError(response);
    expect(error.code).toBe('AssociationMismatch');
    expect(error.status).toBe(422);
    expect(error.traceId).toBe('00-abc-01');
  });

  it('synthesizes a code for authorization rejections, which carry no body', async () => {
    // RequireAuthorization fails before the exception handler runs, so there is nothing to parse.
    const denied = await toApiError(new Response(null, { status: 403 }));
    expect(denied.code).toBe('AccessDenied');
    const missing = await toApiError(new Response(null, { status: 404 }));
    expect(missing.code).toBe('NotFound');
    const anonymous = await toApiError(new Response(null, { status: 401 }));
    expect(anonymous.code).toBe('Unauthenticated');
  });

  it('falls back to the status when the body is not a problem document', async () => {
    const error = await toApiError(new Response('<html>gateway</html>', { status: 503 }));
    expect(error.code).toBe('Unavailable');
    expect(error.title).toContain('unavailable');
  });

  it('keeps the validation code for a rejected payload without an explicit code', async () => {
    const response = new Response(JSON.stringify({ title: 'One or more validation errors occurred.', errors: {} }), {
      status: 400,
    });
    const error = await toApiError(response);
    expect(error.code).toBe('ValidationFailed');
    expect(error.title).toContain('validation');
  });
});
