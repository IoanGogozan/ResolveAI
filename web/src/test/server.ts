import { setupServer } from 'msw/node';
import { defaultHandlers } from './backend';

// Tests override individual endpoints with server.use(); afterEach restores this baseline.
export const server = setupServer(...defaultHandlers);
