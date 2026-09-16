import { useMutation, useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { ApiError } from '@/api/ApiError';
import { useApi } from '@/api/context';
import { useCaller, useIdentityScope } from '@/auth/caller';
import { Code, Fact, Facts, Panel, StatusBadge } from '@/components/primitives';

interface ProbeResult {
  endpoint: string;
  status: number;
  code: string;
}

async function probe(endpoint: string, call: () => Promise<unknown>): Promise<ProbeResult> {
  try {
    await call();
    return { endpoint, status: 200, code: 'Ok' };
  } catch (error) {
    if (error instanceof ApiError) return { endpoint, status: error.status, code: error.code };
    throw error;
  }
}

export function DiagnosticsPage() {
  const api = useApi();
  const scope = useIdentityScope();
  const { data: caller } = useCaller();
  const [caseId, setCaseId] = useState('');

  const live = useQuery({ queryKey: ['health', 'live'], queryFn: () => api.health('live'), retry: false });
  const ready = useQuery({
    queryKey: ['health', 'ready'],
    queryFn: () => api.health('ready'),
    retry: false,
    refetchInterval: 10_000,
  });

  const reads = useMutation<ProbeResult[], Error, string>({
    retry: false,
    mutationFn: (id) =>
      Promise.all([
        probe('GET /api/cases/{id}', () => api.getCase(id)),
        probe('GET /api/cases/{id}/business-facts', () => api.businessFacts(id)),
        probe('GET /api/cases/{id}/proposals', () => api.listProposals(id)),
        probe('GET /api/cases/{id}/audit', () => api.audit(id)),
      ]),
  });

  return (
    <>
      <Panel title="Backend">
        <Facts>
          <Fact label="Process liveness">
            <StatusBadge status={live.isPending ? 'Checking' : live.data ? 'Live' : 'Unreachable'} />
          </Fact>
          <Fact label="Database readiness">
            <StatusBadge status={ready.isPending ? 'Checking' : ready.data ? 'Ready' : 'Unavailable'} />
          </Fact>
          <Fact label="OpenAPI document">
            <a href="/openapi/v1.json" target="_blank" rel="noreferrer">
              /openapi/v1.json
            </a>
          </Fact>
        </Facts>
        <p className="muted">
          Readiness only asserts that PostgreSQL answers. It does not claim that migrations are current.
        </p>
      </Panel>

      <Panel title="Caller context">
        {caller ? (
          <Facts>
            <Fact label="Actor">
              {caller.actorId} ({caller.actorType})
            </Fact>
            <Fact label="Tenant">
              <Code>{caller.tenantId}</Code>
            </Fact>
            <Fact label="Permissions">
              <ul className="permissions">
                {caller.permissions.map((permission) => (
                  <li key={permission}>
                    <code>{permission}</code>
                  </li>
                ))}
              </ul>
            </Fact>
          </Facts>
        ) : (
          <p className="muted">Select an identity to see the context the backend derives for it.</p>
        )}
        <p className="muted">
          This list only decides which controls the console enables. Every endpoint authorizes independently, so removing
          a control never removes a check.
        </p>
      </Panel>

      <Panel title="Enforcement probe">
        <p>
          Run the four case reads against any case identifier under the current identity. Switch identities and repeat to
          see tenant isolation and permission checks answer differently for the same request.
        </p>
        <form
          className="form form--inline"
          onSubmit={(event) => {
            event.preventDefault();
            if (caseId.trim().length > 0) reads.mutate(caseId.trim());
          }}
        >
          <label className="field">
            <span className="field__label">Case ID</span>
            <input
              className="field__input"
              value={caseId}
              placeholder="paste a case identifier"
              onChange={(event) => setCaseId(event.target.value)}
            />
          </label>
          <button type="submit" className="button" disabled={caseId.trim().length === 0 || reads.isPending}>
            {reads.isPending ? 'Probing…' : 'Probe reads'}
          </button>
        </form>
        {reads.data ? (
          <table className="table">
            <thead>
              <tr>
                <th scope="col">Endpoint</th>
                <th scope="col">Status</th>
                <th scope="col">Outcome</th>
              </tr>
            </thead>
            <tbody>
              {reads.data.map((result) => (
                <tr key={result.endpoint}>
                  <td>
                    <code>{result.endpoint}</code>
                  </td>
                  <td>{result.status}</td>
                  <td>
                    <StatusBadge status={result.code} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : null}
        <p className="muted">
          Probed as <code>{scope}</code>. A case from another tenant answers <code>404 NotFound</code>, identical to a
          case that does not exist; the backend does not disclose the difference.
        </p>
      </Panel>
    </>
  );
}
