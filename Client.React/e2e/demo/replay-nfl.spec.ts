/**
 * frizat-703.6: the first browser-driven test proving the complete pick -> live update -> settle
 * flow against REAL ESPN wire values, with actual UI clicks, independent of any live game being
 * in progress. Drives ReplayCacheService through a real captured game (IND @ ATL, real end-of-Q2,
 * mid-Q3, and final OT values — see sample_espn_nfl_*.json at the repo root, frizat-703.5) via the
 * test-only POST /api/replay/advance endpoint.
 *
 * Requires a backend running with DEMO_MODE=true AND DEMO_REPLAY_MODE=true (not just DEMO_MODE) —
 * see the demo-replay-nfl Playwright project and its CI job.
 */
import { test, expect } from '@playwright/test';
import { DEMO_USERS, adminApiContext, demoLogin } from '../helpers/demoAuth';

test('NFL replay — pick, live update, settle against real captured ESPN values', async ({ page, baseURL }) => {
  const admin = await adminApiContext(baseURL!);
  try {
    // The NFL and CFB replay specs share one backend process and one ReplayCacheService sequence
    // (same underlying real captured game, both sports) — reset first so this test doesn't
    // inherit whatever snapshot index a prior run of either spec left behind.
    expect((await admin.post('/api/replay/reset')).ok()).toBe(true);

    await demoLogin(page, DEMO_USERS.alice);
    await page.goto('/picks');

    // ── Scheduled: the real IND @ ATL game is pickable ──────────────────────
    const indButton = page.getByRole('button', { name: /^Pick IND/i });
    await expect(indButton).toBeVisible({ timeout: 15_000 });
    await indButton.click();
    await expect(page.getByRole('button', { name: /IND picked/i })).toBeVisible();

    await page.getByRole('button', { name: /^Submit Pick\(s\)$/i }).click();
    await expect(page.getByRole('button', { name: /IND locked in/i })).toBeVisible({ timeout: 10_000 });

    // ── Advance to halftime (real: IND 13, ATL 14, end of Q2) — scores page updates without reload ──
    // ScoresPage renders halftime the same as any other live state — "Q{period} {clock}" — it has
    // no separate "Half Time" label (see ScoresPage.tsx's isLive branch), so assert the real text.
    expect((await admin.post('/api/replay/advance')).ok()).toBe(true);
    await page.goto('/scores');
    await expect(page.getByText(/Q2.*0:00/i)).toBeVisible({ timeout: 15_000 });
    await expect(page.getByRole('heading', { name: '13', exact: true })).toBeVisible();
    await expect(page.getByRole('heading', { name: '14', exact: true })).toBeVisible();

    // ── Advance to in-progress (real: IND 13, ATL 17, Q3 9:47) ──────────────
    expect((await admin.post('/api/replay/advance')).ok()).toBe(true);
    await page.reload();
    await expect(page.getByText(/Q3.*9:47/i)).toBeVisible({ timeout: 15_000 });
    await expect(page.getByRole('heading', { name: '17', exact: true })).toBeVisible();

    // ── Advance to final (real OT result: IND 31, ATL 25) — leaderboard settles ────
    expect((await admin.post('/api/replay/advance')).ok()).toBe(true);
    await page.reload();
    await expect(page.getByText(/Final/i)).toBeVisible({ timeout: 15_000 });
    await expect(page.getByRole('heading', { name: '31', exact: true })).toBeVisible();
    await expect(page.getByRole('heading', { name: '25', exact: true })).toBeVisible();

    await page.goto('/leaderboard');
    await expect(page.getByRole('heading', { name: /Leaderboard/i })).toBeVisible({ timeout: 15_000 });
  } finally {
    await admin.dispose();
  }
});
