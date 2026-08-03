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

    // ── Replay backend (frizat-703.6) — separate port, DEMO_REPLAY_MODE=true ───
    // Does its own login (no storageState) since it also needs ADMIN_EMAIL/PASSWORD to drive
    // the replay advance endpoint — see e2e/demo/replay-nfl.spec.ts.
    {
      name: 'demo-replay-nfl',
      use: { ...devices['Desktop Chrome'], baseURL: 'http://localhost:5175' },
      testMatch: '**/demo/replay-nfl.spec.ts',
    },
    // Same replay backend (port 5175), CFB subdomain — proves the SSE push path (cfbAdapter),
    // distinct from NFL's poll path above. See e2e/demo/replay-cfb.spec.ts. Depends on
    // demo-replay-nfl rather than running in its own parallel worker: both projects drive the
    // SAME backend process's single ReplayCacheService sequence (one real game, one mutable
    // snapshot index shared by design — see ReplayCacheService.cs), so if they ran concurrently
    // one test's advance/reset could race the other mid-assertion. `dependencies` forces
    // sequential execution regardless of `workers`, not just fullyParallel/CI settings.
    {
      name: 'demo-replay-cfb',
      dependencies: ['demo-replay-nfl'],
      use: { ...devices['Desktop Chrome'], baseURL: 'http://cfb.localhost:5175' },
      testMatch: '**/demo/replay-cfb.spec.ts',
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
