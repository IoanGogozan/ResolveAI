import { Outcome } from '@/components/Outcome';
import { Code, Empty, Fact, Facts, Loading, Money, Panel, StatusBadge } from '@/components/primitives';
import { useBusinessFacts } from './api';

/** `hasReferences` is undefined while the case itself is still loading, which is not the same as "none". */
export function BusinessFactsPanel({ caseId, hasReferences }: { caseId: string; hasReferences: boolean | undefined }) {
  const { data, error, isPending, refetch } = useBusinessFacts(caseId, hasReferences === true);

  return (
    <Panel title="Business facts">
      {hasReferences === undefined ? (
        <Loading what="the case" />
      ) : !hasReferences ? (
        <Empty>This case has no business references, so there are no facts to resolve.</Empty>
      ) : (
        <>
          {isPending ? <Loading what="business facts" /> : null}
          {error ? (
            <Outcome error={error} requiredPermission="payments.read" onRetry={() => void refetch()} />
          ) : null}
          {data ? (
            <div className="columns">
              <Facts>
                <Fact label="Customer">{data.customer.displayName}</Fact>
                <Fact label="Customer ID">
                  <Code>{data.customer.id}</Code>
                </Fact>
              </Facts>
              <Facts>
                <Fact label="Product">{data.order.product}</Fact>
                <Fact label="Order total">
                  <Money amount={data.order.total} currency={data.order.currency} />
                </Fact>
                <Fact label="Delivery">
                  <StatusBadge status={data.order.deliveryStatus} />
                </Fact>
              </Facts>
              <Facts>
                <Fact label="Captured">
                  <Money amount={data.payment.capturedAmount} currency={data.payment.currency} />
                </Fact>
                <Fact label="Refunded">
                  <Money amount={data.payment.refundedAmount} currency={data.payment.currency} />
                </Fact>
                <Fact label="Payment">
                  <StatusBadge status={data.payment.status} />
                </Fact>
              </Facts>
            </div>
          ) : null}
        </>
      )}
    </Panel>
  );
}
