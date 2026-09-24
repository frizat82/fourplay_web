import { test, expect } from '@playwright/test';
import { mockAuth, waitForSpinner, TEST_USER } from './helpers/auth';

/**
 * Regression guard for frizat-2ey: MUI's <CssBaseline/> sets body's background-color from
 * theme.ts's palette.background.default, which must exactly match global.css's --bg-2 (the
 * page background gradient's own bottom stop). If the two drift apart, a visible seam appears
 * below the first viewport in whichever mode diverged (see global.css's body rule comment and
 * the style-guide skill's "Page background gradient" section for the three prior, distinct
 * incidents in this same subsystem). This assertion holds regardless of page height or scroll
 * position, since background-color is a single resolved value applied to the whole body box.
 */
test.describe('Page background color (frizat-2ey regression guard)', () => {
  for (const mode of ['light', 'dark'] as const) {
    test(`${mode} mode: body background-color matches --bg-2`, async ({ page }) => {
      await page.addInitScript(
        (m) => localStorage.setItem('FourPlayWebApp.ThemeMode', m),
        mode,
      );
      await mockAuth(page, { authUser: TEST_USER, navigateTo: '/league/manage' });
      await waitForSpinner(page);
      // The probe div must be attached to document.body — an unattached element does not
      // inherit CSS custom properties (getComputedStyle on it returns '' for backgroundColor),
      // so this can't be simplified to a detached element without silently breaking the check.
      const readColors = () => page.evaluate(() => {
        const bg2Raw = getComputedStyle(document.documentElement).getPropertyValue('--bg-2').trim();
        const probe = document.createElement('div');
        probe.style.backgroundColor = bg2Raw;
        document.body.appendChild(probe);
        const expectedBg = getComputedStyle(probe).backgroundColor;
        probe.remove();
        return { bodyBg: getComputedStyle(document.body).backgroundColor, expectedBg };
      });
      // Polled rather than read once: global.css gives body a 0.3s background-color transition,
      // so a fast page load can be read mid-fade (e.g. rgba(22, 30, 51, 0.68)). A real mismatch
      // never settles and still fails; this only waits out the fade.
      await expect.poll(async () => {
        const { bodyBg, expectedBg } = await readColors();
        return bodyBg === expectedBg ? 'match' : `body ${bodyBg} vs --bg-2 ${expectedBg}`;
      }, { timeout: 3000 }).toBe('match');
    });
  }

  // The dark-mode attribute used to be set only by ThemeModeProvider's effect, after React's
  // first paint — so every dark-mode cold load painted the light background first and then ran
  // body's 0.3s background transition to dark (the mid-fade colour this suite kept catching).
  // It must already be in place when the document finishes parsing, before the app's JS runs.
  test('dark mode is applied before the app script runs (no light-to-dark flash)', async ({ page }) => {
    await page.addInitScript(() => {
      localStorage.setItem('FourPlayWebApp.ThemeMode', 'dark');
      document.addEventListener('readystatechange', () => {
        if (document.readyState === 'interactive') {
          (window as unknown as { __themeAtParse: string | null }).__themeAtParse =
            document.documentElement.getAttribute('data-theme');
        }
      });
    });
    await mockAuth(page, { authUser: TEST_USER, navigateTo: '/league/manage' });

    const themeAtParse = await page.evaluate(() => (window as unknown as { __themeAtParse: string | null }).__themeAtParse);
    expect(themeAtParse).toBe('dark');
  });
});

