import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

const apiTarget = process.env.VITE_API_TARGET ?? 'https://localhost:7209';

// Vercel's own VERCEL_GIT_COMMIT_SHA is auto-injected into the build env for every git-connected
// deploy, but it isn't VITE_-prefixed so Vite won't expose it to client code on its own — bridge
// it into import.meta.env.VITE_APP_VERSION here (frizat-066). VITE_APP_VERSION itself is kept as
// an override for any environment that sets it directly instead.
const appVersion = process.env.VERCEL_GIT_COMMIT_SHA ?? process.env.VITE_APP_VERSION ?? '';

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  define: {
    'import.meta.env.VITE_APP_VERSION': JSON.stringify(appVersion),
  },
  server: {
    proxy: {
      '/api': {
        target: apiTarget,
        changeOrigin: true,
        secure: false, // allow self-signed cert on local dev server
      },
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: './src/setupTests.ts',
    globals: true,
    clearMocks: true,
    restoreMocks: true,
    exclude: ['node_modules', 'e2e/**'],
    coverage: {
      provider: 'v8',
      reporter: ['text', 'json', 'html', 'lcov'],
      // CI gate (npm run test -- --run --coverage): fails the build if frontend coverage drops.
      // Set a few points under the 2026-10-04 baseline (79.8/80.5/73.9/82.3) — raise as it grows.
      thresholds: { statements: 75, branches: 75, functions: 70, lines: 78 },
      exclude: [
        'node_modules/',
        'src/setupTests.ts',
      ],
    },
  },
});
