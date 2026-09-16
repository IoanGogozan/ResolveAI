import { createContext, useContext, useMemo, type ReactNode } from 'react';
import { createApiClient, type Authorize, type ResolveAiApi } from './client';

const ApiContext = createContext<ResolveAiApi | null>(null);

export function ApiProvider({ authorize, children }: { authorize: Authorize; children: ReactNode }) {
  const client = useMemo(() => createApiClient(authorize), [authorize]);
  return <ApiContext.Provider value={client}>{children}</ApiContext.Provider>;
}

export function useApi(): ResolveAiApi {
  const client = useContext(ApiContext);
  if (client === null) throw new Error('useApi requires an ApiProvider.');
  return client;
}
