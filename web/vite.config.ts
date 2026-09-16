import react from '@vitejs/plugin-react';
import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';

// The backend is reached through a same-origin dev proxy, so no CORS policy is needed on the host.
const backend = process.env.RESOLVEAI_BACKEND ?? 'http://localhost:5080';

export default defineConfig({
  plugins: [react()],
  // Anything outside the current directory is imported through this alias, which keeps the module
  // boundary rules in eslint.config.js exact rather than dependent on relative-path depth.
  resolve: { alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) } },
  server: {
    port: 5173,
    proxy: {
      '/api': backend,
      '/health': backend,
      '/openapi': backend,
    },
  },
  test: {
    environment: 'jsdom',
    globals: false,
    setupFiles: ['./src/test/setup.ts'],
    css: false,
    // The identity switcher is compiled out unless this flag is set, so tests must opt in too.
    env: { VITE_DEV_IDENTITIES: 'true' },
  },
});
