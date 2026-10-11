import { test, expect, devices, type Locator, type Page } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { hexToRgb } from '@mui/material/styles';
import { setupRoutes } from './helpers/routes';
import { injectAuthCookie } from './helpers/auth';
import { createAppTheme } from '../src/app/theme';

// Reproduces the iOS "white screen on a weak signal" report: the installed app launched, one
// request stalled, and nothing was ever drawn. Each test stalls or kills one piece of the launch
// and asserts the user still sees something they can act on.

// The app's entry module. A regex, not a glob: after a file change the dev server requests it as
// /src/main.tsx?t=<timestamp>, which '**/src/main.tsx' silently fails to match.
const APP_ENTRY = /\/src\/main\.tsx(\?|$)/;

type Mode = 'light' | 'dark';

/** Loads only index.html's static splash — the app's own code never arrives. */
async function openSplash(page: Page, mode?: Mode) {
  if (mode) await page.addInitScript(m => localStorage.setItem('FourPlayWebApp.ThemeMode', m), mode);
  await page.route(APP_ENTRY, () => {});
  await page.goto('/', { waitUntil: 'commit' });
  const splash = page.getByTestId('boot-splash');
  await expect(splash).toBeVisible();
  return splash;
}

/** Signed in, with the session check either still waiting on the server or unable to reach it. */
async function openApp(page: Page, sessionCheck: 'pending' | 'unreachable') {
  await setupRoutes(page);
  await injectAuthCookie(page);
  await page.route('**/api/auth/me', route => (sessionCheck === 'unreachable' ? route.abort('internetdisconnected') : undefined));
  await page.goto('/dashboard');
}

test.describe('Launch on a bad connection', () => {
  test('a hung Google Fonts request does not block the app from rendering', async ({ page }) => {
    // Never fulfilled — the way a request on a dead connection behaves.
    await page.route(/fonts\.(googleapis|gstatic)\.com/, () => {});
    await page.goto('/', { waitUntil: 'commit' });
    await expect(page.locator('text=IV League').first()).toBeVisible({ timeout: 5000 });
  });

  test('shows a splash, then a retry button, when the app code never arrives', async ({ page }) => {
    await page.clock.install();
    const splash = await openSplash(page);
    await expect(splash.getByRole('button', { name: /retry/i })).toBeHidden();

    await page.clock.fastForward(15_000);
    await expect(splash.getByRole('button', { name: /retry/i })).toBeVisible();
  });

  for (const mode of ['light', 'dark'] as const) {
    // Before the bundled CSS arrives the page must already be the app's own background, not white,
    // and the same color global.css settles on so there's no flash when it takes over.
    test(`${mode} mode: the page is painted the app background before the app code loads`, async ({ page }) => {
      const css = readFileSync(fileURLToPath(new URL('../src/app/global.css', import.meta.url)), 'utf8');
      const block = mode === 'dark' ? css.slice(css.indexOf("[data-theme='dark']")) : css.slice(css.indexOf(':root'));
      const bg2 = block.match(/--bg-2:\s*(#[0-9a-f]{6})/i)![1];

      await openSplash(page, mode);
      expect(await page.evaluate(() => getComputedStyle(document.body).backgroundColor)).toBe(hexToRgb(bg2));
    });

    // index.html can't import theme.ts, so its inline Retry button repeats the theme's primary
    // filled-button colors by hand (/style-guide: theme.ts is the source of truth) — this keeps
    // them from drifting, in both modes.
    test(`${mode} mode: the splash Retry button uses the theme's primary button colors`, async ({ page }) => {
      const primary = createAppTheme(mode).palette.primary;
      await page.clock.install();
      const splash = await openSplash(page, mode);
      await page.clock.fastForward(15_000);
      const retry = splash.getByRole('button', { name: /retry/i });
      await expect(retry).toBeVisible();

      const colors = await retry.evaluate(el => ({ bg: getComputedStyle(el).backgroundColor, fg: getComputedStyle(el).color }));
      expect(colors).toEqual({ bg: hexToRgb(primary.main), fg: hexToRgb(primary.contrastText) });
    });
  }

  test('a signed-in user whose session check cannot reach the server gets a retry prompt, not the login page', async ({ page }) => {
    await openApp(page, 'unreachable');
    await expect(page.getByText(/can't reach iv league/i)).toBeVisible();
    await expect(page).not.toHaveURL(/login/);
  });
});

// Launch hands off from the static splash (index.html) to the app's own screens: a spinner while
// the session check runs, then the "Can't reach" prompt if it fails. Every one of them must sit
// dead centre on a phone — the app's used to sit near the top, so the screen visibly jumped.
test.describe('Launch screens stay centred (iPhone)', () => {
  test.use({ viewport: devices['iPhone 13'].viewport });

  async function expectCentred(target: Locator) {
    await expect(target).toBeVisible();
    const box = (await target.boundingBox())!;
    const vp = target.page().viewportSize()!;
    const off = {
      dx: Math.abs(Math.round(box.x + box.width / 2 - vp.width / 2)),
      dy: Math.abs(Math.round(box.y + box.height / 2 - vp.height / 2)),
    };
    expect(off.dx <= 2 && off.dy <= 2, `off centre by ${JSON.stringify(off)}`).toBe(true);
  }

  test('the splash spinner', async ({ page }) => {
    const splash = await openSplash(page);
    await expectCentred(splash.getByRole('progressbar'));
  });

  test("the app's spinner while the session check runs", async ({ page }) => {
    await openApp(page, 'pending');
    await expect(page.getByTestId('boot-splash')).toHaveCount(0); // the app has taken over
    await expectCentred(page.getByRole('progressbar'));
  });

  test('the "Can\'t reach IV League" prompt when the session check fails', async ({ page }) => {
    await openApp(page, 'unreachable');
    await expect(page.getByText(/can't reach iv league/i)).toBeVisible();
    await expectCentred(page.getByTestId('launch-screen').locator(':scope > *'));
  });

  // Same spinner on both sides of the hand-off: the splash copies what the theme's CircularProgress
  // renders — the same SVG arc, colour per mode from theme.ts, full opacity (/style-guide), 40px.
  for (const mode of ['light', 'dark'] as const) {
    test(`${mode} mode: the splash spinner is the app's spinner`, async ({ page }) => {
      const theme = createAppTheme(mode);
      const key = theme.components!.MuiCircularProgress!.defaultProps!.color as 'primary' | 'secondary';
      const splash = await openSplash(page, mode);
      const spinner = splash.getByRole('progressbar');
      const style = await spinner.evaluate(el => ({
        stroke: getComputedStyle(el.querySelector('circle')!).stroke,
        opacity: getComputedStyle(el).opacity,
        size: (el as HTMLElement).offsetWidth, // layout size: a rotating spinner's bounding box grows mid-turn
      }));
      expect(style).toEqual({ stroke: hexToRgb(theme.palette[key].main), opacity: '1', size: 40 });
    });
  }
});
