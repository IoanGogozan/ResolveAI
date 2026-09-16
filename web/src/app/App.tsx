import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useCallback, type ReactNode } from 'react';
import { Navigate, Route, Routes } from 'react-router-dom';
import { ApiProvider } from '@/api/context';
import { IdentityProvider, useIdentity } from '@/auth/identity';
import { CaseListPage } from '@/features/cases/CaseListPage';
import { NewCasePage } from '@/features/cases/NewCasePage';
import { DiagnosticsPage } from '@/features/diagnostics/DiagnosticsPage';
import { AppShell } from './AppShell';
import { CaseDetailPage } from './CaseDetailPage';
import { ErrorBoundary } from './ErrorBoundary';

export function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        // A refused request is a deliberate backend answer, not a glitch to retry into a different one.
        retry: false,
        refetchOnWindowFocus: false,
      },
    },
  });
}

/** Wires identity to the api client. Only this file knows that both exist. */
export function App({ queryClient }: { queryClient: QueryClient }) {
  const clearCachedData = useCallback(() => queryClient.clear(), [queryClient]);
  return (
    <ErrorBoundary>
      <QueryClientProvider client={queryClient}>
        <IdentityProvider onIdentityChanged={clearCachedData}>
          <AuthorizedApi>
            <Routes>
              <Route element={<AppShell />} path="/">
                <Route index element={<CaseListPage />} />
                <Route path="cases/new" element={<NewCasePage />} />
                <Route path="cases/:id" element={<CaseDetailPage />} />
                <Route path="diagnostics" element={<DiagnosticsPage />} />
                <Route path="*" element={<Navigate to="/" replace />} />
              </Route>
            </Routes>
          </AuthorizedApi>
        </IdentityProvider>
      </QueryClientProvider>
    </ErrorBoundary>
  );
}

function AuthorizedApi({ children }: { children: ReactNode }) {
  const { authorize } = useIdentity();
  return <ApiProvider authorize={authorize}>{children}</ApiProvider>;
}
