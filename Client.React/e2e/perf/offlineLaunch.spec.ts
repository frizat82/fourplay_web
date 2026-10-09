import { test, expect } from '@playwright/test';

// The iOS report: launching the installed app on a weak signal gave a blank white screen until
// force-quit. The service worker only runs in a production build, so this lives with the perf
// specs (vite preview), not the dev-server mock suite. Run with `npm run test:perf`.
test.describe('Offline launch (app-shell service worker)', () => {
  test('opens from the saved copy with no connection, then offers a retry', async ({ page, context }) => {
    await page.goto('/');
    // First visit installs the worker and saves the page + its build files.
    await page.evaluate(async () => {
      await navigator.serviceWorker.ready;
      if (!navigator.serviceWorker.controller) {
        await new Promise(resolve => navigator.serviceWorker.addEventListener('controllerchange', resolve, { once: true }));
      }
    });

    await context.setOffline(true);
    await page.reload();

    // Not a browser error page and not a blank screen: the app itself, telling the user what's up.
    await expect(page.getByText(/can't reach iv league/i)).toBeVisible();
    await expect(page.getByRole('button', { name: /retry/i })).toBeVisible();
  });

  test('never serves an API call from the cache', async ({ page }) => {
    await page.goto('/');
    await page.evaluate(() => navigator.serviceWorker.ready);
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
