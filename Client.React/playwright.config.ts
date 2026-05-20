import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: 'html',
  use: {
    baseURL: 'http://localhost:5173',
    trace: 'on-first-retry',
  },
  projects: [
    // ── Mock-based tests (no real backend needed) ──────────────────────────────
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
      testMatch: /^(?!.*[\\/]demo[\\/]).*\.spec\.ts$/,
    },

    // ── Demo backend — setup (login once, save cookies) ────────────────────────
    {
      name: 'demo-setup-nfl',
      use: { ...devices['Desktop Chrome'], baseURL: 'http://localhost:5174' },
      testMatch: '**/demo/setup.nfl.ts',
    },
    {
      name: 'demo-setup-cfb',
      use: { ...devices['Desktop Chrome'], baseURL: 'http://cfb.localhost:5174' },
      testMatch: '**/demo/setup.cfb.ts',
    },

    // ── Demo backend — NFL tests ───────────────────────────────────────────────
    {
      name: 'demo-nfl',
      dependencies: ['demo-setup-nfl'],
      use: {
        ...devices['Desktop Chrome'],
        baseURL: 'http://localhost:5174',
        storageState: 'e2e/demo/.auth/alice-nfl.json',
      },
      testMatch: ['**/demo/nfl-*.spec.ts', '**/demo/leaderboard.spec.ts'],
    },

    // ── Demo backend — CFB tests ───────────────────────────────────────────────
    {
      name: 'demo-cfb',
      dependencies: ['demo-setup-cfb'],
      use: {
        ...devices['Desktop Chrome'],
        baseURL: 'http://cfb.localhost:5174',
        storageState: 'e2e/demo/.auth/alice-cfb.json',
      },
      testMatch: '**/demo/cfb-*.spec.ts',
    },
  ],
  // In CI, auto-start the dev server on port 5173 (mock-based chromium tests only).
  // Demo tests (demo-nfl / demo-cfb) require a running DEMO_MODE=true backend — run locally.
  webServer: process.env.CI ? {
    command: 'VITE_API_TARGET=http://localhost:9999 npm run dev -- --port 5173',
    url: 'http://localhost:5173',
    reuseExistingServer: false,
    timeout: 60_000,
  } : undefined,
});
