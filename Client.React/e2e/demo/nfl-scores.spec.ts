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

  // REGRESSION: in-progress game must show "Live" indicator
  test('Super Bowl shows Live indicator', async ({ page }) => {
    await expect(page.getByText('Live')).toBeVisible({ timeout: 8_000 });
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
    await page.getByRole('option', { name: 'Wild Card' }).click();
    await waitForSpinner(page);

    // Should have green and red badges — no "default" (which means homeCovers=null/missing spreads)
    const successBadges = page.locator('.MuiBadge-colorSuccess');
    const errorBadges = page.locator('.MuiBadge-colorError');
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
