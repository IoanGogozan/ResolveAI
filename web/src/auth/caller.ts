import { useQuery } from '@tanstack/react-query';
import { useApi } from '@/api/context';
import type { CallerContext } from '@/api/types';
import { useIdentity } from './identity';

/**
 * The caller's own trusted context, used only to enable or disable controls. The backend still
 * authorizes every request, so a stale or missing permission list can never grant access.
 */
export function useCaller() {
  const api = useApi();
  const { selected } = useIdentity();
  return useQuery<CallerContext>({
    queryKey: ['caller', selected],
    queryFn: () => api.caller(),
    enabled: selected !== null,
    retry: false,
    staleTime: 5 * 60 * 1000,
  });
}

export function hasPermission(caller: CallerContext | undefined, permission: string): boolean {
  return caller?.permissions.includes(permission) ?? false;
}

/**
 * `resolved` separates "this identity lacks the permission" from "the context has not arrived yet".
 * Without it a panel would accuse the backend of refusing something it was never asked.
 */
export function usePermission(permission: string): { granted: boolean; resolved: boolean } {
  const { data, isLoading } = useCaller();
  return { granted: hasPermission(data, permission), resolved: !isLoading };
}

/** Query keys are scoped by identity so a cache entry can never be read under another actor. */
export function useIdentityScope(): string {
  return useIdentity().selected ?? 'anonymous';
}
