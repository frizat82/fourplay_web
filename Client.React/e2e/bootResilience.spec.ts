import { test, expect, devices, type Page } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { setupRoutes } from './helpers/routes';
import { injectAuthCookie } from './helpers/auth';
import { createAppTheme } from '../src/app/theme';

// Reproduces the iOS "white screen on a weak signal" report: the installed app launched, one
// request stalled, and nothing was ever drawn. Each test stalls or kills one piece of the launch
// and asserts the user still sees something they can act on.
// The app's entry module. A regex, not a glob: after a file change the dev server requests it as
// /src/main.tsx?t=<timestamp>, which '**/src/main.tsx' silently fails to match.
const APP_ENTRY = /\/src\/main\.tsx(\?|$)/;

test.describe('Launch on a bad connection', () => {
  test('a hung Google Fonts request does not block the app from rendering', async ({ page }) => {
    // Never fulfilled — the way a request on a dead connection behaves.
    await page.route(/fonts\.(googleapis|gstatic)\.com/, () => {});
    await page.goto('/', { waitUntil: 'commit' });
    await expect(page.locator('text=IV League').first()).toBeVisible({ timeout: 5000 });
  });

  test('shows a splash, then a retry button, when the app code never arrives', async ({ page }) => {
    await page.clock.install();
    await page.route(APP_ENTRY, () => {});
    await page.goto('/', { waitUntil: 'commit' });

    const splash = page.getByTestId('boot-splash');
    await expect(splash).toBeVisible();
    await expect(splash.getByRole('button', { name: /retry/i })).toBeHidden();

    await page.clock.fastForward(15_000);
    await expect(splash.getByRole('button', { name: /retry/i })).toBeVisible();
  });

  // Before the bundled CSS arrives the page must already be the app's own background, not white,
  // and the same color global.css settles on so there's no flash when it takes over.
  for (const mode of ['light', 'dark'] as const) {
    test(`${mode} mode: the page is painted the app background before the app code loads`, async ({ page }) => {
      const css = readFileSync(fileURLToPath(new URL('../src/app/global.css', import.meta.url)), 'utf8');
      const block = mode === 'dark' ? css.slice(css.indexOf("[data-theme='dark']")) : css.slice(css.indexOf(':root'));
      const bg2 = block.match(/--bg-2:\s*(#[0-9a-f]{6})/i)![1];
      const expected = `rgb(${[1, 3, 5].map(i => parseInt(bg2.slice(i, i + 2), 16)).join(', ')})`;

      await page.addInitScript(m => localStorage.setItem('FourPlayWebApp.ThemeMode', m), mode);
      await page.route(APP_ENTRY, () => {});
      await page.goto('/', { waitUntil: 'commit' });
      await expect(page.getByTestId('boot-splash')).toBeVisible();

      expect(await page.evaluate(() => getComputedStyle(document.body).backgroundColor)).toBe(expected);
    });

    // index.html can't import theme.ts, so its inline Retry button repeats the theme's primary
    // filled-button colors by hand (/style-guide: theme.ts is the source of truth) — this keeps
    // them from drifting, in both modes.
    test(`${mode} mode: the splash Retry button uses the theme's primary button colors`, async ({ page }) => {
      const primary = createAppTheme(mode).palette.primary;
      const toRgb = (hex: string) => `rgb(${[1, 3, 5].map(i => parseInt(hex.slice(i, i + 2), 16)).join(', ')})`;

      await page.clock.install();
      await page.addInitScript(m => localStorage.setItem('FourPlayWebApp.ThemeMode', m), mode);
      await page.route(APP_ENTRY, () => {});
      await page.goto('/', { waitUntil: 'commit' });
      await page.clock.fastForward(15_000);
      const retry = page.getByTestId('boot-splash').getByRole('button', { name: /retry/i });
      await expect(retry).toBeVisible();

      const colors = await retry.evaluate(el => ({ bg: getComputedStyle(el).backgroundColor, fg: getComputedStyle(el).color }));
      expect(colors).toEqual({ bg: toRgb(primary.main), fg: toRgb(primary.contrastText) });
    });
  }

  test('a signed-in user whose session check cannot reach the server gets a retry prompt, not the login page', async ({ page }) => {
    await setupRoutes(page);
    await injectAuthCookie(page);
    await page.route('**/api/auth/me', route => route.abort('internetdisconnected'));

    await page.goto('/dashboard');

    await expect(page.getByText(/can't reach iv league/i)).toBeVisible();
    await expect(page).not.toHaveURL(/login/);
  });
});

// The launch hands off from the static splash (index.html) to the app's own loading screen while
// the session check runs. Both spinners must sit dead centre, in the same spot, on a phone — the
// app's used to sit near the top, so the spinner visibly jumped on every launch.
test.describe('Launch spinner position (iPhone)', () => {
  test.use({ viewport: devices['iPhone 13'].viewport });

  async function spinnerOffsetFromCentre(page: Page) {
    const spinner = page.getByRole('progressbar').first();
    await expect(spinner).toBeVisible();
    const box = (await spinner.boundingBox())!;
    const vp = page.viewportSize()!;
    return {
      dx: Math.round(box.x + box.width / 2 - vp.width / 2),
      dy: Math.round(box.y + box.height / 2 - vp.height / 2),
    };
  }

  test('the splash spinner is centred', async ({ page }) => {
    await page.route(APP_ENTRY, () => {});
    await page.goto('/', { waitUntil: 'commit' });
    await expect(page.getByTestId('boot-splash')).toBeVisible();
    const { dx, dy } = await spinnerOffsetFromCentre(page);
    expect(Math.abs(dx)).toBeLessThanOrEqual(2);
    expect(Math.abs(dy)).toBeLessThanOrEqual(2);
  });

  // Same spinner on both sides of the hand-off: the splash copies the theme's CircularProgress
  // (colour per mode from theme.ts, full opacity per /style-guide, same 40px size).
  for (const mode of ['light', 'dark'] as const) {
    test(`${mode} mode: the splash spinner matches the app's spinner`, async ({ page }) => {
      const theme = createAppTheme(mode);
      const key = theme.components!.MuiCircularProgress!.defaultProps!.color as 'primary' | 'secondary';
      const hex = theme.palette[key].main;
      const rgb = `rgb(${[1, 3, 5].map(i => parseInt(hex.slice(i, i + 2), 16)).join(', ')})`;

      await page.addInitScript(m => localStorage.setItem('FourPlayWebApp.ThemeMode', m), mode);
      await page.route(APP_ENTRY, () => {});
      await page.goto('/', { waitUntil: 'commit' });
      const spinner = page.getByTestId('boot-splash').getByRole('progressbar');
      const style = await spinner.evaluate(el => {
        const cs = getComputedStyle(el);
        return { color: cs.borderTopColor, opacity: cs.opacity, size: (el as HTMLElement).offsetWidth }; // layout size: a rotating spinner's bounding box grows mid-turn
      });
      expect(style).toEqual({ color: rgb, opacity: '1', size: 40 });
    });
  }

  test("the app's loading spinner, while the session check runs, is centred too", async ({ page }) => {
    await setupRoutes(page);
    await injectAuthCookie(page);
    await page.route('**/api/auth/me', () => {}); // still waiting on the server
    await page.goto('/dashboard');
    await expect(page.getByTestId('boot-splash')).toHaveCount(0); // the app has taken over
    const { dx, dy } = await spinnerOffsetFromCentre(page);
    expect(Math.abs(dx)).toBeLessThanOrEqual(2);
    expect(Math.abs(dy)).toBeLessThanOrEqual(2);
  });
});

