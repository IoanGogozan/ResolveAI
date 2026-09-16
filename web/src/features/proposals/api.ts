import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useApi } from '@/api/context';
import { isRunFinished, type ProposalRun } from '@/api/types';
import { useIdentityScope } from '@/auth/caller';

/**
 * The host resolves a claim whose lease has expired when the runs are read, so polling this list is
 * what turns an abandoned invocation into an explicit `Interrupted / OutcomeUnknown` outcome.
 * A run therefore cannot appear to be active forever, and nothing is retried to achieve that.
 */
export const pollingCeilingMs = 150_000;

export function pollingInterval(runs: ProposalRun[] | undefined, now: number = Date.now()): number | false {
  const active = runs?.find((run) => !isRunFinished(run));
  if (active === undefined) return false;
  const elapsed = now - Date.parse(active.createdAt);
  if (Number.isNaN(elapsed)) return 2000;
  if (elapsed > pollingCeilingMs) return false;
  if (elapsed < 5000) return 1000;
  if (elapsed < 15000) return 2000;
  return 5000;
}

export function useProposalRuns(caseId: string, enabled: boolean) {
  const api = useApi();
  const scope = useIdentityScope();
  return useQuery<ProposalRun[]>({
    queryKey: ['proposals', scope, caseId],
    queryFn: () => api.listProposals(caseId),
    enabled,
    retry: false,
    refetchInterval: (query) => pollingInterval(query.state.data),
  });
}

export function useGenerateProposal(caseId: string) {
  const api = useApi();
  const scope = useIdentityScope();
  const queryClient = useQueryClient();
  return useMutation<ProposalRun, Error, string>({
    // A failed attempt must never be retried automatically: the invocation may already have been
    // billed. Recovery replays the same operation ID only when the operator asks for it.
    retry: false,
    mutationFn: (operationId) => api.generateProposal(caseId, operationId),
    onSettled: () => queryClient.invalidateQueries({ queryKey: ['proposals', scope, caseId] }),
  });
}
