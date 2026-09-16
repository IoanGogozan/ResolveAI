import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Outcome } from '@/components/Outcome';
import { Empty, Instant, Loading, Panel, StatusBadge } from '@/components/primitives';
import { hasPermission, useCaller } from '@/auth/caller';
import { useCaseList } from './api';

export function CaseListPage() {
  // Cursors are opaque; keeping the visited ones lets the operator page back without a server call.
  const [cursors, setCursors] = useState<(string | null)[]>([null]);
  const cursor = cursors[cursors.length - 1] ?? null;
  const { data, error, isPending, refetch } = useCaseList(cursor);
  const { data: caller } = useCaller();
  const canCreate = hasPermission(caller, 'cases.create');

  return (
    <Panel
      title="Cases"
      actions={
        canCreate ? (
          <Link className="button" to="/cases/new">
            New case
          </Link>
        ) : (
          <span className="muted" title="Requires the cases.create permission">
            Creating cases requires <code>cases.create</code>
          </span>
        )
      }
    >
      {isPending ? <Loading what="cases" /> : null}
      {error ? <Outcome error={error} requiredPermission="cases.read" onRetry={() => void refetch()} /> : null}
      {data ? (
        data.items.length === 0 ? (
          <Empty>No cases exist for this identity yet.</Empty>
        ) : (
          <>
            <table className="table">
              <thead>
                <tr>
                  <th scope="col">Subject</th>
                  <th scope="col">Status</th>
                  <th scope="col">References</th>
                  <th scope="col">Created</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((item) => (
                  <tr key={item.id}>
                    <td>
                      <Link to={`/cases/${item.id}`}>{item.subject}</Link>
                    </td>
                    <td>
                      <StatusBadge status={item.status} />
                    </td>
                    <td>{item.references ? item.references.paymentId : <span className="muted">none</span>}</td>
                    <td>
                      <Instant value={item.createdAt} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            <div className="pager">
              <button
                type="button"
                className="button button--quiet"
                disabled={cursors.length === 1}
                onClick={() => setCursors((visited) => visited.slice(0, -1))}
              >
                Previous
              </button>
              <button
                type="button"
                className="button button--quiet"
                disabled={data.nextCursor === null}
                onClick={() => setCursors((visited) => [...visited, data.nextCursor])}
              >
                Next
              </button>
            </div>
          </>
        )
      ) : null}
    </Panel>
  );
}
