import { Link, useParams } from 'react-router-dom';
import { AuditTimeline } from '@/features/audit/AuditTimeline';
import { BusinessFactsPanel } from '@/features/business-facts/BusinessFactsPanel';
import { useCase } from '@/features/cases/api';
import { CaseOverview } from '@/features/cases/CaseOverview';
import { ProposalPanel } from '@/features/proposals/ProposalPanel';

/**
 * Cross-feature composition lives in app/, mirroring how Orchestration composes backend modules
 * through their contracts instead of letting modules call each other directly.
 */
export function CaseDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { data, isPending, isError } = useCase(id ?? '');

  if (id === undefined) return null;

  return (
    <>
      <Link className="backlink" to="/">
        ← All cases
      </Link>
      <CaseOverview caseId={id} />
      {/* Without a visible case there is nothing for the dependent panels to describe, and repeating
          the same refusal in each of them would only obscure it. */}
      {isError ? null : (
        <>
          <BusinessFactsPanel caseId={id} hasReferences={isPending ? undefined : data?.references != null} />
          <ProposalPanel caseId={id} />
          <AuditTimeline caseId={id} />
        </>
      )}
    </>
  );
}
