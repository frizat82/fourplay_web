import { test, expect, devices, type Browser } from '@playwright/test';
import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { setupRoutes } from '../helpers/routes';
import { injectAuthCookie } from '../helpers/auth';
import { createPick } from '../../src/test/fixtures';

// Wall-clock A/B between two production builds — "did this change make launch slower?". Skipped
// unless PERF_COMPARE_BEFORE points at another checkout's Client.React (with its own `vite build`
// already run); see the /perf-check skill for the full recipe. perf.spec.ts gates the deterministic
// metrics on every run; this answers the timing question it deliberately doesn't.
//
// Both builds get identical mocked APIs (150ms each), an iPhone viewport, and two network profiles.
// Each run is a fresh context: a first visit (no service worker yet), then a reload once the worker
// has installed (repeat visit — what an installed home-screen app does on every launch).
const BEFORE_DIR = process.env.PERF_COMPARE_BEFORE;
const AFTER_DIR = fileURLToPath(new URL('../..', import.meta.url));
const RUNS = Number(process.env.PERF_COMPARE_RUNS ?? 5);
const API_LATENCY_MS = 150;

const NETWORKS = [
  { name: 'fast', conditions: null },
  // Roughly a weak cellular signal: 300ms round trips, 1.5 Mbps down.
  { name: 'slow', conditions: { offline: false, latency: 300, downloadThroughput: (1.5 * 1024 * 1024) / 8, uploadThroughput: (750 * 1024) / 8 } },
] as const;

test.describe.configure({ mode: 'serial' });
test.skip(!BEFORE_DIR, 'set PERF_COMPARE_BEFORE to a built Client.React checkout to compare against');

async function startPreview(dir: string, port: number) {
  const proc = spawn('npx', ['vite', 'preview', '--port', String(port), '--strictPort'], { cwd: dir, stdio: 'ignore', detached: true });
  const base = `http://localhost:${port}`;
  for (let i = 0; i < 100; i++) {
    try {
      if ((await fetch(base)).ok) return { base, stop: () => { try { process.kill(-proc.pid!); } catch { /* already exited */ } } };
    } catch { /* not up yet */ }
    await new Promise(r => setTimeout(r, 100));
  }
  throw new Error(`vite preview did not start in ${dir}`);
}

async function timeLaunches(browser: Browser, base: string, network: (typeof NETWORKS)[number]) {
  const first: number[] = [];
  const repeat: number[] = [];
  for (let run = 0; run < RUNS; run++) {
    const ctx = await browser.newContext({ viewport: devices['iPhone 13'].viewport });
    const page = await ctx.newPage();
    if (network.conditions) {
      const cdp = await ctx.newCDPSession(page);
      await cdp.send('Network.enable');
      await cdp.send('Network.emulateNetworkConditions', network.conditions);
    }
    await setupRoutes(page, { userPicks: [createPick({ team: 'BUF', userId: 'test-user-id-001' })] });
    await page.route('**/api/**', async r => { await new Promise(res => setTimeout(res, API_LATENCY_MS)); await r.fallback(); });
    await page.route(url => !['localhost', '127.0.0.1'].includes(url.hostname), r => r.abort());
    await injectAuthCookie(page);
    const ready = () => expect(page.getByTestId('picks-island').getByText('BUF')).toBeVisible({ timeout: 30_000 });

    let t = Date.now();
    await page.goto(`${base}/dashboard`);
    await ready();
    first.push(Date.now() - t);

    await page.evaluate(() => new Promise(r => setTimeout(r, 1500))); // let the worker install
    t = Date.now();
    await page.reload();
    await ready();
    repeat.push(Date.now() - t);
    await ctx.close();
  }
  return { first, repeat };
}

const median = (xs: number[]) => [...xs].sort((a, b) => a - b)[Math.floor(xs.length / 2)];

test('launch timing: before vs after', async ({ browser }) => {
  test.setTimeout(10 * 60_000);
  const builds = [
    { name: 'before', ...(await startPreview(BEFORE_DIR!, 4301)) },
    { name: 'after', ...(await startPreview(AFTER_DIR, 4302)) },
  ];
  try {
    for (const network of NETWORKS) {
      for (const b of builds) {
        const { first, repeat } = await timeLaunches(browser, b.base, network);
        console.log(`[compare] ${network.name.padEnd(4)} ${b.name.padEnd(6)} first-visit median ${median(first)}ms ${JSON.stringify(first)} | repeat-visit median ${median(repeat)}ms ${JSON.stringify(repeat)}`);
      }
    }
  } finally {
    builds.forEach(b => b.stop());
  }
});
