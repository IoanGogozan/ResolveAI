import { Outcome } from '@/components/Outcome';
import { Code, Fact, Facts, Instant, Loading, Panel, StatusBadge } from '@/components/primitives';
import { useCase } from './api';

export function CaseOverview({ caseId }: { caseId: string }) {
  const { data, error, isPending } = useCase(caseId);

  return (
    <Panel title="Case">
      {isPending ? <Loading what="the case" /> : null}
      {error ? <Outcome error={error} requiredPermission="cases.read" /> : null}
      {data ? (
        <>
          <h3 className="subject">{data.subject}</h3>
          <p className="description">{data.description}</p>
          <Facts>
            <Fact label="Status">
              <StatusBadge status={data.status} />
            </Fact>
            <Fact label="Case ID">
              <Code>{data.id}</Code>
            </Fact>
            <Fact label="Created">
              <Instant value={data.createdAt} />
            </Fact>
            <Fact label="References">
              {data.references ? (
                <span>
                  <Code>{data.references.customerId}</Code> / <Code>{data.references.orderId}</Code> /{' '}
                  <Code>{data.references.paymentId}</Code>
                </span>
              ) : (
                <span className="muted">
                  None. References can only be supplied at creation time; the backend has no endpoint to add them later.
                </span>
              )}
            </Fact>
          </Facts>
        </>
      ) : null}
    </Panel>
  );
}
