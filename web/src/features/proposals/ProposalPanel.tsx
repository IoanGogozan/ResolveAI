import { useEffect, useState } from 'react';
import { isRunFinished, type ProposalRun } from '@/api/types';
import { usePermission } from '@/auth/caller';
import { Outcome } from '@/components/Outcome';
import { Code, Empty, Fact, Facts, Instant, Loading, Money, Panel, StatusBadge } from '@/components/primitives';
import { formatCostUsd, formatDuration } from '@/lib/format';
import { pollingCeilingMs, useGenerateProposal, useProposalRuns } from './api';
import { claimOperation, readPendingOperation, releaseOperation, startNewOperation } from './operations';

export function ProposalPanel({ caseId }: { caseId: string }) {
  const { granted: canRead, resolved } = usePermission('proposals.read');
  const { granted: canCreate } = usePermission('proposals.create');
  const runs = useProposalRuns(caseId, canRead);
  const generate = useGenerateProposal(caseId);
  const [pending, setPending] = useState(() => readPendingOperation(caseId));

  const active = runs.data?.find((run) => !isRunFinished(run));
  const pendingRun = pending ? runs.data?.find((run) => run.operationId === pending.operationId) : undefined;

  // The claim is released once its run is terminal, which is also what makes "Generate again" safe.
  useEffect(() => {
    if (pending !== null && pendingRun !== undefined && isRunFinished(pendingRun)) {
      releaseOperation(caseId);
      setPending(null);
    }
  }, [caseId, pending, pendingRun]);

  const stalled = active !== undefined && Date.now() - Date.parse(active.createdAt) > pollingCeilingMs;
  const unresolved = pending !== null && generate.isError;

  function start(mint: () => { operationId: string }) {
    const operation = mint();
    setPending(readPendingOperation(caseId));
    generate.mutate(operation.operationId);
  }

  function generateAgain() {
    const confirmed = window.confirm(
      'This starts a new provider invocation with a new operation ID and may incur a new charge. Replaying the previous operation would have been free. Continue?',
    );
    if (confirmed) start(() => startNewOperation(caseId));
  }

  return (
    <Panel
      title="AI proposal"
      actions={
        !resolved ? null : canCreate ? (
          runs.data && runs.data.length > 0 && active === undefined && !unresolved ? (
            <button type="button" className="button button--quiet" onClick={generateAgain} disabled={generate.isPending}>
              Generate again
            </button>
          ) : active === undefined && !unresolved ? (
            <button
              type="button"
              className="button"
              onClick={() => start(() => claimOperation(caseId))}
              disabled={generate.isPending}
            >
              {generate.isPending ? 'Requesting…' : 'Generate proposal'}
            </button>
          ) : null
        ) : (
          <span className="muted">
            Generating requires <code>proposals.create</code>
          </span>
        )
      }
    >
      <p className="notice">
        A proposal is a model suggestion. It carries no authority: policy evaluation, approval and refund execution are
        later backend deliveries and no control here can execute anything.
      </p>

      {unresolved ? (
        <div className="outcome outcome--warn" role="alert">
          <div className="outcome__header">
            <span className="outcome__code">Result not received</span>
          </div>
          <p className="outcome__explanation">
            Operation <code>{pending.operationId}</code> was started but its result never arrived. The invocation may
            already have been billed. Replaying the same operation returns the stored result without buying another call.
          </p>
          <button type="button" className="button" onClick={() => generate.mutate(pending.operationId)}>
            Replay this operation
          </button>
        </div>
      ) : null}

      {generate.error && !unresolved ? <Outcome error={generate.error} requiredPermission="proposals.create" /> : null}
      {!resolved ? <Loading what="the caller context" /> : null}
      {resolved && !canRead ? (
        <Empty>
          Reading proposals requires <code>proposals.read</code> together with the three business-read permissions.
        </Empty>
      ) : null}
      {runs.isPending && canRead ? <Loading what="proposal runs" /> : null}
      {runs.error ? <Outcome error={runs.error} requiredPermission="proposals.read" /> : null}

      {active !== undefined ? (
        <p className="muted" role="status">
          {stalled
            ? 'The run has exceeded its lease window. Reload the page: the backend resolves an abandoned claim to an explicit uncertain outcome rather than reissuing the call.'
            : 'Invocation in progress. Polling for the persisted result…'}
        </p>
      ) : null}

      {runs.data && runs.data.length === 0 ? <Empty>No proposal has been requested for this case.</Empty> : null}
      {runs.data?.map((run, index) => <RunCard key={run.runId} run={run} latest={index === 0} />)}
    </Panel>
  );
}

function RunCard({ run, latest }: { run: ProposalRun; latest: boolean }) {
  const [showMetadata, setShowMetadata] = useState(latest);

  return (
    <article className={`run ${latest ? 'run--latest' : ''}`}>
      <header className="run__header">
        <StatusBadge status={run.status} />
        {run.errorCode ? <span className="run__error">{run.errorCode}</span> : null}
        <span className="muted">
          <Instant value={run.createdAt} />
        </span>
        <button type="button" className="button button--link" onClick={() => setShowMetadata((shown) => !shown)}>
          {showMetadata ? 'Hide invocation metadata' : 'Show invocation metadata'}
        </button>
      </header>

      {run.errorCode === 'ProviderNotConfigured' ? (
        <p className="notice notice--warn">
          The host has no provider API key configured, so no invocation was attempted. This is the expected local state
          without credentials, not a model failure.
        </p>
      ) : null}
      {run.status === 'Interrupted' ? (
        <p className="notice notice--warn">
          The outcome is uncertain. The backend deliberately records uncertainty instead of silently reissuing a paid
          call, so nothing here retries on its own.
        </p>
      ) : null}

      {run.proposal ? (
        <Facts>
          <Fact label="Proposed action">{run.proposal.action}</Fact>
          <Fact label="Amount">
            {run.proposal.amount !== null && run.proposal.currency !== null ? (
              <Money amount={run.proposal.amount} currency={run.proposal.currency} />
            ) : (
              <span className="muted">none</span>
            )}
          </Fact>
          <Fact label="Reason">{run.proposal.reasonCode}</Fact>
          <Fact label="Payment">
            {run.proposal.paymentId ? <Code>{run.proposal.paymentId}</Code> : <span className="muted">none</span>}
          </Fact>
          <Fact label="Model confidence">
            {/* Shown as a model attribute only. Confidence never grants authority. */}
            {run.proposal.confidence.toFixed(2)}
          </Fact>
          <Fact label="Evidence">
            <ul className="evidence">
              {run.proposal.evidence.map((item) => (
                <li key={item}>{item}</li>
              ))}
            </ul>
          </Fact>
        </Facts>
      ) : null}

      {showMetadata ? (
        <Facts>
          <Fact label="Provider / model">
            {run.provider} · {run.model}
          </Fact>
          <Fact label="Prompt / schema version">
            {run.promptVersion} · {run.schemaVersion}
          </Fact>
          <Fact label="Tokens">
            {run.inputTokens ?? '—'} in / {run.outputTokens ?? '—'} out
          </Fact>
          <Fact label="Estimated cost">
            {run.estimatedCostUsd !== null ? formatCostUsd(run.estimatedCostUsd) : <span className="muted">unknown</span>}
          </Fact>
          <Fact label="Latency">
            {run.latencyMs !== null ? formatDuration(run.latencyMs) : <span className="muted">unknown</span>}
          </Fact>
          <Fact label="Operation ID">
            <Code>{run.operationId}</Code>
          </Fact>
        </Facts>
      ) : null}
    </article>
  );
}
