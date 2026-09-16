import { usePermission } from '@/auth/caller';
import { Outcome } from '@/components/Outcome';
import { Empty, Instant, Loading, Panel } from '@/components/primitives';
import { shortId } from '@/lib/format';
import { useAudit } from './api';

export function AuditTimeline({ caseId }: { caseId: string }) {
  const { granted: canRead, resolved } = usePermission('audit.read');
  const { data, error, isPending } = useAudit(caseId, canRead);

  return (
    <Panel title="Audit timeline">
      {!resolved ? <Loading what="the caller context" /> : null}
      {resolved && !canRead ? (
        <Empty>
          Reading audit requires <code>audit.read</code>, which is deliberately more restrictive than case access.
        </Empty>
      ) : null}
      {isPending && canRead ? <Loading what="audit events" /> : null}
      {error ? <Outcome error={error} requiredPermission="audit.read" /> : null}
      {data && data.length === 0 ? (
        <p className="muted" role="status">
          No events delivered yet. Audit is written in the same transaction as the change and published by a background
          worker, normally within about a second.
        </p>
      ) : null}
      {data && data.length > 0 ? (
        <ol className="timeline">
          {data.map((fact) => (
            <li key={fact.eventId} className="timeline__item">
              <span className="timeline__action">{fact.action}</span>
              <span className="timeline__meta">
                <Instant value={fact.occurredAt} /> · {fact.actorId} ({fact.actorType})
              </span>
              {fact.traceParent ? (
                <span className="timeline__trace" title={fact.traceParent}>
                  trace {shortId(fact.traceParent)}
                </span>
              ) : null}
            </li>
          ))}
        </ol>
      ) : null}
    </Panel>
  );
}
