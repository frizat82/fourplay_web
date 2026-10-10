import { test, expect } from '@playwright/test';
import { spawn, type ChildProcess } from 'node:child_process';

// The iOS report: launching the installed app on a weak signal gave a blank white screen until
// force-quit. The service worker only runs in a production build, so this lives with the perf
// specs (served from the `vite build` output), not the dev-server mock suite. Run with
// `npm run test:perf`.
//
// Each test runs its own `vite preview` and kills it to go offline. Playwright's setOffline() is
// not reliable for this: a service worker that restarts mid-test can come back without the
// emulated-offline flag and quietly fetch from the still-running server — which once let this
// test pass while every cache lookup was actually missing.
let server: ChildProcess | undefined;
let base = '';

async function startServer(port: number): Promise<void> {
  server = spawn('npx', ['vite', 'preview', '--port', String(port), '--strictPort'], { stdio: 'ignore', detached: true });
  base = `http://localhost:${port}`;
  for (let i = 0; i < 100; i++) {
    try {
      if ((await fetch(base)).ok) return;
    } catch { /* not up yet */ }
    await new Promise(r => setTimeout(r, 100));
  }
  throw new Error('vite preview did not start');
}

function stopServer(): void {
  if (server?.pid) process.kill(-server.pid); // the whole npx → vite process group
  server = undefined;
}

async function waitForWorker(page: import('@playwright/test').Page): Promise<void> {
  await page.evaluate(async () => {
    await navigator.serviceWorker.ready;
    if (!navigator.serviceWorker.controller) {
      await new Promise(resolve => navigator.serviceWorker.addEventListener('controllerchange', resolve, { once: true }));
    }
  });
}

test.describe('Offline launch (app-shell service worker)', () => {
  test.describe.configure({ mode: 'serial' });
  test.afterEach(stopServer);

  test('opens from the saved copy with the server gone, then offers a retry', async ({ page }, testInfo) => {
    await startServer(4180 + testInfo.repeatEachIndex % 50);
    await page.goto(base);
    await waitForWorker(page); // first visit installs the worker and saves the page + its files

    stopServer();
    await page.reload();

    // Not a browser error page and not a blank screen: the app itself, telling the user what's up.
    await expect(page.getByText(/can't reach iv league/i)).toBeVisible();
    await expect(page.getByRole('button', { name: /retry/i })).toBeVisible();
  });

  test('never serves an API call from the cache', async ({ page }, testInfo) => {
    await startServer(4230 + testInfo.repeatEachIndex % 50);
    await page.goto(base);
    await waitForWorker(page);
    const cached = await page.evaluate(async () => {
      const urls: string[] = [];
      for (const name of await caches.keys()) {
        const cache = await caches.open(name);
        for (const request of await cache.keys()) urls.push(new URL(request.url).pathname);
      }
      return urls;
    });
    expect(cached.length).toBeGreaterThan(0);
    expect(cached.filter(path => path.startsWith('/api/'))).toEqual([]);
  });
});
