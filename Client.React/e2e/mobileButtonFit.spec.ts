import { test, expect, devices, type Page } from '@playwright/test';
import { mockAuth } from './helpers/auth';

// Primary audience is iOS Safari at ~390px. A button whose label is wider than its own content
// box renders text flush against (or past) both edges, and one that wraps to two lines looks just
// as broken — this guards every visible button on the main pages against both.

async function badButtons(page: Page) {
  return page.evaluate(() =>
    [...document.querySelectorAll<HTMLElement>('button, a.MuiButton-root')]
      .filter((b) => b.getClientRects().length > 0 && getComputedStyle(b).visibility !== 'hidden' && b.textContent?.trim())
      .map((b) => {
        // Measure the label text only — MUI's start/end icons deliberately sit 4px into the
        // padding, so including them would flag every icon button.
        const cs = getComputedStyle(b);
        const box = b.getBoundingClientRect();
        const walker = document.createTreeWalker(b, NodeFilter.SHOW_TEXT);
        const rects: DOMRect[] = [];
        for (let n = walker.nextNode(); n; n = walker.nextNode()) {
          if (!n.textContent?.trim() || n.parentElement?.closest('.MuiButton-startIcon, .MuiButton-endIcon')) continue;
          const range = document.createRange();
          range.selectNodeContents(n);
          rects.push(...range.getClientRects());
        }
        const left = Math.min(...rects.map((r) => r.left));
        const right = Math.max(...rects.map((r) => r.right));
        const lines = new Set(rects.map((r) => Math.round(r.top))).size;
        return {
          text: b.textContent!.trim(),
          overflows: left < box.left + parseFloat(cs.paddingLeft) - 1 || right > box.right - parseFloat(cs.paddingRight) + 1,
          wraps: lines > 1,
        };
      })
      .filter((b) => b.overflows || b.wraps)
  );
}

test.describe('Button labels fit inside their buttons on an iPhone-width screen', () => {
  test.use({ viewport: devices['iPhone 13'].viewport });

  // Each page's own button must be rendered before measuring — otherwise only the nav's icon
  // buttons exist yet and the check passes vacuously. Pages with no reliable text button wait
  // for the mocked API calls to settle instead.
  for (const [path, readyButton] of [
    ['/', null],
    ['/dashboard', null],
    ['/picks', null],
    ['/scores', null],
    ['/leaderboard', null],
    ['/rules', null],
    ['/league/manage', null],
    ['/account/manage', /^notifications$/i],
    ['/account/manage/notifications', /save preferences/i],
    ['/account/manage/changepassword', /change password|update password|save/i],
    ['/account/manage/changeusername', /change username|update username|save/i],
  ] as const) {
    test(path, async ({ page }) => {
      await mockAuth(page, { navigateTo: path });
      if (readyButton) await expect(page.getByRole('button', { name: readyButton }).first()).toBeVisible();
      else await page.waitForLoadState('networkidle');
      expect(await badButtons(page)).toEqual([]);
    });
  }
});
