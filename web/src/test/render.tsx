import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import type { ReactNode } from 'react';
import { MemoryRouter } from 'react-router-dom';
import { ApiProvider } from '@/api/context';
import { IdentityProvider } from '@/auth/identity';

export function renderWithBackend(ui: ReactNode, { identity = 'alpha-operator', route = '/' } = {}) {
  sessionStorage.setItem('resolveai.identity', identity);
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const view = render(
    <QueryClientProvider client={queryClient}>
      <IdentityProvider>
        <ApiProvider authorize={(headers) => headers.set('X-Development-User', identity)}>
          <MemoryRouter initialEntries={[route]}>{ui}</MemoryRouter>
        </ApiProvider>
      </IdentityProvider>
    </QueryClientProvider>,
  );
  return { ...view, queryClient };
}
