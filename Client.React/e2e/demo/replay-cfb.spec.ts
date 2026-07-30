/**
 * frizat-703.6: CFB variant of replay-nfl.spec.ts — same real captured IND @ ATL game (see
 * sample_espn_nfl_*.json at the repo root, frizat-703.5), replayed through ReplayCacheService and
 * added as a second game inside the REAL CFP Championship slate (surfaced via CFB's normal
 * slate-based picks/scores flow — cfb.localhost). It's added to the real slate rather than a new
 * one because CfbCurrentSlateService can only ever resolve an already-fully-seeded season, and the
 * WeekYearSelector clamps any out-of-range slate/week back to the real Championship anyway (see
 * DemoDataSeeder.SeedReplayCfbSlateAsync for the full reasoning). Runs as the admin user, not a
 * demo user — every demo user already has a real Championship pick, which would leave zero
 * required picks left for IND; admin has none.
 *
 * Where the NFL spec advances by navigating/reloading, this spec stays on the scores page across
 * an advance to prove the SSE push path (/api/cfb/live-stream, cfbAdapter's sseUrl) updates the
 * page without any reload — NFL's poll path is covered separately by replay-nfl.spec.ts.
 *
 * Requires a backend running with DEMO_MODE=true AND DEMO_REPLAY_MODE=true — see the
 * demo-replay-cfb Playwright project and its CI job.
 */
import { test, expect } from '@playwright/test';
import { adminApiContext, adminCreds, demoLogin } from '../helpers/demoAuth';

test('CFB replay — pick, live SSE update, settle against real captured ESPN values', async ({ page, baseURL }) => {
  const admin = await adminApiContext(baseURL!);
  try {
    // Shares one ReplayCacheService sequence with the NFL spec (same underlying real game, both
    // sports) — reset first so this test doesn't inherit whatever index the NFL spec left behind.
    expect((await admin.post('/api/replay/reset')).ok()).toBe(true);

    await demoLogin(page, adminCreds());
    await page.goto('/picks');

    // ── Scheduled: the real IND @ ATL game is pickable (replay slate — 1 required pick) ──
    const indButton = page.getByRole('button', { name: /^Pick IND/i });
    await expect(indButton).toBeVisible({ timeout: 15_000 });
    await indButton.click();
    await expect(page.getByRole('button', { name: /IND picked/i })).toBeVisible();

    await page.getByRole('button', { name: /^Submit Pick\(s\)$/i }).click();
    await expect(page.getByRole('button', { name: /IND locked in/i })).toBeVisible({ timeout: 10_000 });

    // The Championship slate also has the real IU @ MIA game (14-7) — scope every score assertion
    // to the IND @ ATL card specifically so e.g. its "14" isn't ambiguous with IU's real "14".
    const indCard = page.locator('.MuiPaper-root').filter({ hasText: 'IND' });

    // Note: unlike NFL, CFB doesn't assert on the "Q{period} {clock}" text — cfbAdapter's live
    // down/distance/clock display (game.situation) comes from a separate getLiveGames() feed that
    // has no entry for this replay game, so it falls back to a static placeholder
    // (CFB_DEMO_SITUATION) regardless of the actual replay state. That's a pre-existing CFB data
    // gap, not something this spec is proving — the score values and gameStatus-derived "Final"
    // text below DO come straight from the real replay data via the shared toGameStatus
    // (gameHelpers.ts), so those are what's asserted here.

    // ── Advance to halftime (real: IND 13, ATL 14, end of Q2) — first load reflects it directly ──
    expect((await admin.post('/api/replay/advance')).ok()).toBe(true);
    await page.goto('/scores');
    await expect(indCard.getByRole('heading', { name: '13', exact: true })).toBeVisible({ timeout: 15_000 });
    await expect(indCard.getByRole('heading', { name: '14', exact: true })).toBeVisible();

    // ── Advance to in-progress (real: IND 13, ATL 17) WITHOUT reloading — halftime already made
    // hasActiveGames=true, so the SSE connection is open; this proves the push path updates the
    // page on its own, not a fresh navigation re-fetching current state. ──
    expect((await admin.post('/api/replay/advance')).ok()).toBe(true);
    await expect(indCard.getByRole('heading', { name: '17', exact: true })).toBeVisible({ timeout: 15_000 });

    // ── Advance to final (real OT result: IND 31, ATL 25) — again via SSE push, no reload ────
    expect((await admin.post('/api/replay/advance')).ok()).toBe(true);
    await expect(indCard.getByText(/Final/i)).toBeVisible({ timeout: 15_000 });
    await expect(indCard.getByRole('heading', { name: '31', exact: true })).toBeVisible();
    await expect(indCard.getByRole('heading', { name: '25', exact: true })).toBeVisible();

    await page.goto('/leaderboard');
    await expect(page.getByRole('heading', { name: /Leaderboard/i })).toBeVisible({ timeout: 15_000 });
  } finally {
    await admin.dispose();
  }
});
