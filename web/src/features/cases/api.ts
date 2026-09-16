import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useApi } from '@/api/context';
import type { CasePage, CaseSummary, CreateCaseRequest } from '@/api/types';
import { useIdentityScope } from '@/auth/caller';

export function useCaseList(cursor: string | null) {
  const api = useApi();
  const scope = useIdentityScope();
  return useQuery<CasePage>({
    queryKey: ['cases', scope, 'list', cursor],
    queryFn: () => api.listCases({ limit: 20, cursor }),
    retry: false,
  });
}

export function useCase(id: string) {
  const api = useApi();
  const scope = useIdentityScope();
  return useQuery<CaseSummary>({
    queryKey: ['cases', scope, id],
    queryFn: () => api.getCase(id),
    retry: false,
  });
}

export function useCreateCase() {
  const api = useApi();
  const scope = useIdentityScope();
  const queryClient = useQueryClient();
  return useMutation<CaseSummary, Error, CreateCaseRequest>({
    // Case creation has no server-side idempotency key yet, so a retry could create a second case.
    // The form prevents a second submission instead of retrying automatically.
    retry: false,
    mutationFn: (request) => api.createCase(request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['cases', scope, 'list'] }),
  });
}
