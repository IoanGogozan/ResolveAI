import { useQuery } from '@tanstack/react-query';
import { useApi } from '@/api/context';
import type { AuditFact } from '@/api/types';
import { useIdentityScope } from '@/auth/caller';

/**
 * Audit is delivered by a background worker after the originating transaction commits, so an empty
 * result shortly after a mutation means "not delivered yet". Polling briefly keeps that distinction
 * visible instead of showing an empty timeline that looks like lost data.
 */
export function useAudit(caseId: string, enabled: boolean) {
  const api = useApi();
  const scope = useIdentityScope();
  return useQuery<AuditFact[]>({
    queryKey: ['audit', scope, caseId],
    queryFn: () => api.audit(caseId),
    enabled,
    retry: false,
    refetchInterval: (query) => (query.state.data?.length === 0 ? 1500 : false),
  });
}
