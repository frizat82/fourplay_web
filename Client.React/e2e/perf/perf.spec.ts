import { test, expect, type Page } from '@playwright/test';
import { readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { setupRoutes } from '../helpers/routes';
import { injectAuthCookie } from '../helpers/auth';
import { createPick } from '../../src/test/fixtures';

// Cold-load performance baseline, modeled on claude.dev's "measure first" approach: prefer
// deterministic numbers (bytes, request counts, request-chain depth, layout shift) over
// wall-clock time, which is too noisy to gate on. Runs against a PRODUCTION build (`vite build`
// + `vite preview`) at an iPhone-sized viewport — the dev server serves unbundled modules and
// says nothing about what a real phone downloads.
//
// Every /api call gets a fixed artificial delay standing in for the Vercel → Railway round
// trip, so API_LATENCY_MS × chainDepth approximates how long a user stares at a skeleton.
//
// Update the baseline after an intentional improvement with: PERF_UPDATE_BASELINE=1 npm run test:perf

const API_LATENCY_MS = 150;
const BASELINE_PATH = fileURLToPath(new URL('./baseline.json', import.meta.url));

interface RouteMetrics {
  jsBytes: number;
  cssBytes: number;
  thirdPartyRequests: number;
  apiRequests: number;
  apiChainDepth: number;
  cls: number;
  shiftsAfterReady: number;
  timeToReadyMs: number;
}

interface ApiTiming { url: string; start: number; end: number }

const isLocal = (url: URL) => url.hostname === 'localhost' || url.hostname === '127.0.0.1';

const ROUTES: { name: string; path: string; ready: (page: Page) => Promise<void> }[] = [
  {
    name: 'dashboard',
    path: '/dashboard',
    ready: page => expect(page.getByTestId('picks-island').getByText('BUF')).toBeVisible(),
  },
  {
    name: 'picks',
    path: '/picks',
    ready: page => expect(page.getByRole('button', { name: /^Pick \w/ }).first()).toBeVisible(),
  },
  {
    name: 'scores',
    path: '/scores',
    ready: page => expect(page.getByText('BUF').first()).toBeVisible(),
  },
];

// Layout-shift observer installed before any page script runs. Each entry is timestamped so
// shifts that land AFTER the page looked ready can be counted separately — the class of jank
// the claude.dev post found passing the CLS threshold while still moving content under users.
const CLS_INIT_SCRIPT = `
  window.__shifts = [];
  new PerformanceObserver(list => {
    for (const e of list.getEntries()) {
      if (!e.hadRecentInput) window.__shifts.push({ value: e.value, time: e.startTime });
    }
  }).observe({ type: 'layout-shift', buffered: true });
`;

// Request-chain depth: a request's depth is 1 + the deepest request that had already finished
// when it started. Depth 1 = fired immediately; depth N = had to wait for N-1 round trips first.
function chainDepth(timings: ApiTiming[]): number {
  const depths: number[] = [];
  timings.forEach((t, i) => {
    let d = 1;
    timings.forEach((o, j) => {
      if (j !== i && o.end <= t.start && depths[j] !== undefined) d = Math.max(d, depths[j] + 1);
    });
    depths[i] = d;
  });
  return Math.max(0, ...depths);
}

async function measure(page: Page, path: string, ready: (page: Page) => Promise<void>): Promise<RouteMetrics> {
  await setupRoutes(page, {
    userPicks: [createPick({ team: 'BUF', userId: 'test-user-id-001' })],
  });
  // Registered after setupRoutes so it runs first, then falls through to the mock.
  await page.route('**/api/**', async route => {
    await new Promise(r => setTimeout(r, API_LATENCY_MS));
    await route.fallback();
  });
  // Third-party origins are counted, then aborted — keeps the run hermetic and deterministic.
  await page.route(url => !isLocal(url), route => route.abort());

  await injectAuthCookie(page);
  await page.addInitScript(CLS_INIT_SCRIPT);

  let jsBytes = 0;
  let cssBytes = 0;
  let thirdPartyRequests = 0;
  const pending = new Map<string, number>();
  const api: ApiTiming[] = [];
  const t0 = Date.now();

  page.on('request', req => {
    const url = new URL(req.url());
    if (!isLocal(url)) thirdPartyRequests++;
    if (url.pathname.startsWith('/api/') && !url.pathname.includes('live-stream')) pending.set(req.url() + '#' + req.method(), Date.now() - t0);
  });
  page.on('requestfinished', async req => {
    const key = req.url() + '#' + req.method();
    const start = pending.get(key);
    if (start !== undefined) {
      api.push({ url: req.url(), start, end: Date.now() - t0 });
      pending.delete(key);
    }
    const res = await req.response();
    if (!res) return;
    const type = req.resourceType();
    if (type === 'script' || type === 'stylesheet') {
      const size = (await res.body().catch(() => Buffer.alloc(0))).length;
      if (type === 'script') jsBytes += size; else cssBytes += size;
    }
  });

  await page.goto(path);
  await ready(page);
  const timeToReadyMs = Date.now() - t0;
  // Bytes needed to reach "ready" — snapshotted now so idle preloads afterwards (App.tsx warms the
  // Leaderboard chunk) don't count as cold-load weight.
  const readyJsBytes = jsBytes;
  const readyCssBytes = cssBytes;
  const readyPerfTime = await page.evaluate(() => performance.now());

  // Let late renders (background queries, banners) settle so shifts after "ready" are caught.
  await page.waitForTimeout(1500);

  const shifts = await page.evaluate(() => (window as unknown as { __shifts: { value: number; time: number }[] }).__shifts);
  const cls = shifts.reduce((s, e) => s + e.value, 0);
  const shiftsAfterReady = shifts.filter(e => e.time > readyPerfTime).length;

  if (process.env.PERF_DEBUG) {
    for (const t of [...api].sort((a, b) => a.start - b.start)) {
      console.log(`  ${String(t.start).padStart(5)}–${String(t.end).padStart(5)}ms  ${new URL(t.url).pathname}`);
    }
  }

  return {
    jsBytes: readyJsBytes,
    cssBytes: readyCssBytes,
    thirdPartyRequests,
    apiRequests: api.length,
    apiChainDepth: chainDepth(api),
    cls: Math.round(cls * 1000) / 1000,
    shiftsAfterReady,
    timeToReadyMs,
  };
}

const results: Record<string, RouteMetrics> = {};

test.describe.configure({ mode: 'serial' });

for (const route of ROUTES) {
  test(`cold load: ${route.name}`, async ({ page }) => {
    const m = await measure(page, route.path, route.ready);
    results[route.name] = m;
    console.log(`[perf] ${route.name}: ${JSON.stringify(m)}`);

    if (process.env.PERF_UPDATE_BASELINE) return;

    const baseline = JSON.parse(readFileSync(BASELINE_PATH, 'utf8')) as Record<string, Omit<RouteMetrics, 'timeToReadyMs'>>;
    const b = baseline[route.name];
    expect(b, `no baseline for ${route.name} — run with PERF_UPDATE_BASELINE=1`).toBeDefined();
    // Deterministic metrics gate; timeToReadyMs is reported only (wall clock is noisy).
    expect(m.jsBytes, 'JS bytes grew >5% over baseline').toBeLessThanOrEqual(Math.ceil(b.jsBytes * 1.05));
    expect(m.cssBytes, 'CSS bytes grew >5% over baseline').toBeLessThanOrEqual(Math.ceil(b.cssBytes * 1.05));
    expect(m.thirdPartyRequests, 'new third-party requests').toBeLessThanOrEqual(b.thirdPartyRequests);
    expect(m.apiChainDepth, 'API request chain got deeper').toBeLessThanOrEqual(b.apiChainDepth);
    expect(m.apiRequests, 'more API requests on cold load').toBeLessThanOrEqual(b.apiRequests);
    expect(m.cls, 'CLS regressed').toBeLessThanOrEqual(b.cls + 0.01);
    expect(m.shiftsAfterReady, 'new layout shifts after the page looked ready').toBeLessThanOrEqual(b.shiftsAfterReady);
  });
}

test.afterAll(() => {
  if (process.env.PERF_UPDATE_BASELINE && Object.keys(results).length === ROUTES.length) {
    // timeToReadyMs is wall clock — logged per run, never persisted, so baseline updates don't churn.
    const persisted = Object.fromEntries(Object.entries(results).map(([k, { timeToReadyMs: _, ...rest }]) => [k, rest]));
    writeFileSync(BASELINE_PATH, JSON.stringify(persisted, null, 2) + '\n');
    console.log(`[perf] baseline written to ${BASELINE_PATH}`);
  }
});
