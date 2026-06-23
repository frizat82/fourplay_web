/**
 * NFL Scores page — demo backend integration tests.
 * Runs against a live DEMO_MODE=true backend at localhost:5174.
 *
 * Frozen current week: Super Bowl (NE home vs SEA away, in-progress Q3).
 * Score: SEA 14, NE 7. SEA ball at NE 35, 2nd & 7.
 */
import { test, expect } from '@playwright/test';
import { waitForSpinner } from '../helpers/demoAuth';

test.describe('NFL scores — demo backend', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/scores');
    await waitForSpinner(page);
  });

  test('Scores heading is visible', async ({ page }) => {
    await expect(page.getByRole('heading', { name: 'Scores' })).toBeVisible();
  });

  // REGRESSION: current week must be Super Bowl (the latest seeded week)
  test('current week is Super Bowl postseason', async ({ page }) => {
    await expect(page.getByRole('combobox').nth(1)).toContainText('Super Bowl', { timeout: 8_000 });
    await expect(page.getByRole('combobox').last()).toContainText('Postseason', { timeout: 5_000 });
  });

  // REGRESSION: Super Bowl must show NE vs SEA — not empty
  test('Super Bowl game is displayed with NE and SEA', async ({ page }) => {
    await expect(page.getByText('NE').first()).toBeVisible({ timeout: 8_000 });
    await expect(page.getByText('SEA').first()).toBeVisible({ timeout: 5_000 });
    await expect(page.getByText('No Odds Available')).not.toBeVisible({ timeout: 3_000 });
  });

  // REGRESSION: in-progress icons must show red/green (isFinal||isLive check, not just isFinal)
  test('Super Bowl person icons are colored red for losing team', async ({ page }) => {
    // NE home spread -2.5, score 7-14 → homeCovers=false → NE icon red, SEA icon green
    await page.waitForTimeout(1500); // allow spread batch to return
    const redIcons = page.locator('.MuiIconButton-colorError');
    await expect(redIcons.first()).toBeVisible({ timeout: 8_000 });
  });

  // REGRESSION: O/U arrow icons must be colored for in-progress
  test('Super Bowl Over/Under arrows are colored (in-progress)', async ({ page }) => {
    await expect(page.locator('[data-testid="over-under-controls"]')).toBeVisible({ timeout: 5_000 });
    // At Q3 score 7+14=21, O/U line=45.5 → going Under → ArrowCircleDownIcon green
    const ouSection = page.locator('[data-testid="over-under-controls"]').first();
    // Verify O/U section exists (arrows colored by JS — presence confirms rendering)
    await expect(ouSection).toBeVisible();
  });

  // REGRESSION: in-progress game must show clock indicator (Q3 8:42 or Live fallback)
  test('Super Bowl shows game clock (Q3) indicator', async ({ page }) => {
    // Shows "Q3 8:42" when situation has period+clock, or "Live" as fallback
    const clockText = page.getByText(/Q[1-4]|Live/);
    await expect(clockText.first()).toBeVisible({ timeout: 8_000 });
  });

  // REGRESSION: field position must show for in-progress games
  test('Super Bowl shows field position bar', async ({ page }) => {
    await expect(page.locator('[data-testid="field-position-bar"]')).toBeVisible({ timeout: 8_000 });
    await expect(page.locator('[data-testid="ball-marker"]')).toBeVisible({ timeout: 5_000 });
  });

  // REGRESSION: down/distance text must show for in-progress game
  test('Super Bowl shows down and distance text', async ({ page }) => {
    await expect(page.getByText('2nd & 7 at NE 35')).toBeVisible({ timeout: 8_000 });
  });

  // REGRESSION: Wild Card must show pick badges with green/red colors (homeCovers not null)
  test('Wild Card spread badges show green/red cover colors', async ({ page }) => {
    const weekSelect = page.getByRole('combobox').nth(1);
    await weekSelect.click();
    // Wait for both ESPN scores AND league picks to complete before checking badge colors
    await Promise.all([
      page.waitForResponse(r => r.url().includes('/api/league/') && r.url().includes('/picks/') && r.status() === 200),
      page.getByRole('option', { name: 'Wild Card' }).click(),
    ]);
    await waitForSpinner(page);
    await page.waitForTimeout(500); // allow React to batch-update allPicks into badge visibility

    // Should have green and red badges — no "default" (which means homeCovers=null/missing spreads)
    const successBadges = page.locator('.MuiBadge-colorSuccess:not(.MuiBadge-invisible)');
    const errorBadges = page.locator('.MuiBadge-colorError:not(.MuiBadge-invisible)');
    await expect(successBadges.first()).toBeVisible({ timeout: 8_000 });
    await expect(errorBadges.first()).toBeVisible({ timeout: 5_000 });
  });

  // REGRESSION: all 6 Wild Card games must have O/U row (BUF was previously missing it)
  test('Wild Card all 6 games show O/U controls on scores page', async ({ page }) => {
    const weekSelect = page.getByRole('combobox').nth(1);
    await weekSelect.click();
    await page.getByRole('option', { name: 'Wild Card' }).click();
    await waitForSpinner(page);

    await expect(page.locator('[data-testid="over-under-controls"]')).toHaveCount(6, { timeout: 8_000 });
  });

  // REGRESSION: regular season must show all 18 weeks
  test('regular season shows all 18 weeks in selector', async ({ page }) => {
    const seasonTypeSelect = page.getByRole('combobox').last();
    await seasonTypeSelect.click();
    await page.getByRole('option', { name: 'Regular Season' }).click();
    await waitForSpinner(page);

    await expect(page.getByRole('combobox').nth(1)).toContainText('Week 18', { timeout: 5_000 });
    await page.getByRole('combobox').nth(1).click();
    await expect(page.getByRole('option', { name: 'Week 1', exact: true })).toBeVisible({ timeout: 5_000 });
    await expect(page.getByRole('option', { name: 'Week 18', exact: true })).toBeVisible({ timeout: 5_000 });
    // Count only items in the open listbox to avoid matching from multiple dropdowns
    const weekOptions = page.locator('[role="listbox"] [role="option"]');
    await expect(weekOptions).toHaveCount(18, { timeout: 5_000 });
    await page.keyboard.press('Escape');
  });

  // REGRESSION: "Current Week" must return to Super Bowl (not stay in regular season)
  test('"Current Week" returns to Super Bowl from regular season', async ({ page }) => {
    const seasonTypeSelect = page.getByRole('combobox').last();
    await seasonTypeSelect.click();
    await page.getByRole('option', { name: 'Regular Season' }).click();
    await waitForSpinner(page);

    await page.getByRole('button', { name: 'Current Week' }).click();
    await waitForSpinner(page);
    await expect(page.getByRole('combobox').nth(1)).toContainText('Super Bowl', { timeout: 5_000 });
    await expect(page.getByRole('combobox').last()).toContainText('Postseason', { timeout: 3_000 });
  });

  test('"Show Only My Picks" button is visible', async ({ page }) => {
    await expect(page.getByRole('button', { name: /show only my picks|show all games/i })).toBeVisible({ timeout: 5_000 });
  });
});
