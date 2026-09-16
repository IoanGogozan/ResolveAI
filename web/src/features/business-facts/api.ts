import { useQuery } from '@tanstack/react-query';
import { useApi } from '@/api/context';
import type { CaseBusinessFacts } from '@/api/types';
import { useIdentityScope } from '@/auth/caller';

export function useBusinessFacts(caseId: string, enabled: boolean) {
  const api = useApi();
  const scope = useIdentityScope();
  return useQuery<CaseBusinessFacts>({
    queryKey: ['business-facts', scope, caseId],
    queryFn: () => api.businessFacts(caseId),
    enabled,
    // The endpoint revalidates the stored references on every call, so a refusal is a real answer
    // about the current data and must not be retried into a different one.
    retry: false,
  });
}
