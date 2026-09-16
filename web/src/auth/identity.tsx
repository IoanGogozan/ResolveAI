import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react';
import type { Authorize } from '@/api/client';

/**
 * The only module that knows how a request is authenticated. Replacing the development header with
 * an OIDC bearer token in V1 changes this file and nothing in features/.
 */

export interface DevelopmentIdentity {
  id: string;
  label: string;
  tenant: string;
  note: string;
}

/**
 * The host maps these fixture names to a tenant and a permission set. The client never sends a
 * tenant or a permission: it selects an identity and the backend derives everything else.
 */
export const developmentIdentities: readonly DevelopmentIdentity[] = [
  {
    id: 'alpha-operator',
    label: 'alpha-operator',
    tenant: 'Alpha',
    note: 'Full read plus case creation and proposal generation.',
  },
  {
    id: 'alpha-reader',
    label: 'alpha-reader',
    tenant: 'Alpha',
    note: 'Case read only; every other capability must be refused by the backend.',
  },
  {
    id: 'beta-operator',
    label: 'beta-operator',
    tenant: 'Beta',
    note: 'Same permissions as alpha-operator, in the other tenant.',
  },
];

export const developmentIdentitiesEnabled = import.meta.env.VITE_DEV_IDENTITIES === 'true';

// The host refuses to start when development authentication is enabled outside Development/Testing.
// The client mirrors that refusal rather than shipping a header-based login to a real deployment.
if (developmentIdentitiesEnabled && import.meta.env.PROD) {
  throw new Error('Development identities must never be enabled in a production build.');
}

const storageKey = 'resolveai.identity';

interface IdentityState {
  /** Null when no identity is selected, which the shell treats as "not signed in". */
  selected: string | null;
  select: (identity: string | null) => void;
  authorize: Authorize;
}

const IdentityContext = createContext<IdentityState | null>(null);

function readStoredIdentity(): string | null {
  if (!developmentIdentitiesEnabled) return null;
  const stored = sessionStorage.getItem(storageKey);
  return developmentIdentities.some((identity) => identity.id === stored) ? stored : null;
}

export function IdentityProvider({
  children,
  onIdentityChanged,
}: {
  children: ReactNode;
  onIdentityChanged?: () => void;
}) {
  const [selected, setSelected] = useState<string | null>(readStoredIdentity);

  const select = useCallback(
    (identity: string | null) => {
      if (identity === null) sessionStorage.removeItem(storageKey);
      else sessionStorage.setItem(storageKey, identity);
      setSelected(identity);
      // Nothing cached under the previous identity may be shown to the next one.
      onIdentityChanged?.();
    },
    [onIdentityChanged],
  );

  const authorize = useCallback<Authorize>(
    (headers) => {
      if (developmentIdentitiesEnabled && selected !== null) headers.set('X-Development-User', selected);
    },
    [selected],
  );

  const value = useMemo<IdentityState>(() => ({ selected, select, authorize }), [selected, select, authorize]);
  return <IdentityContext.Provider value={value}>{children}</IdentityContext.Provider>;
}

export function useIdentity(): IdentityState {
  const identity = useContext(IdentityContext);
  if (identity === null) throw new Error('useIdentity requires an IdentityProvider.');
  return identity;
}
