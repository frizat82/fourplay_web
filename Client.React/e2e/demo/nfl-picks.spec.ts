/**
 * NFL Picks page — demo backend integration tests.
 * Runs against a live DEMO_MODE=true backend at localhost:5174.
 *
 * Frozen current week: Super Bowl (postseason week 5, NE vs SEA in-progress).
 * Alice's SB pick: SEA (away). Alice's Wild Card picks: LAR,CHI,BUF,SF,NE,HOU.
 * Alice's Week 18 picks: BUF, DAL, MIN, MIA.
 */
import { test, expect } from '@playwright/test';
import { waitForSpinner } from '../helpers/demoAuth';

test.describe('NFL picks — demo backend', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/picks');
    await waitForSpinner(page);
  });

  test('Picks heading is visible', async ({ page }) => {
    await expect(page.getByRole('heading', { name: 'Picks', exact: true })).toBeVisible();
  });

  // REGRESSION: current week must be the LATEST (Super Bowl), not Wild Card or Week 18
  test('current week is Super Bowl postseason — the latest seeded week', async ({ page }) => {
    await expect(page.getByRole('combobox').nth(1)).toContainText('Super Bowl', { timeout: 8_000 });
    await expect(page.getByRole('combobox').last()).toContainText('Postseason', { timeout: 5_000 });
  });

  // REGRESSION: Super Bowl must not be empty
  test('Super Bowl shows NE vs SEA game card', async ({ page }) => {
    await expect(page.getByText('NE').first()).toBeVisible({ timeout: 8_000 });
    await expect(page.getByText('SEA').first()).toBeVisible({ timeout: 5_000 });
    await expect(page.getByText('No Odds Available')).not.toBeVisible({ timeout: 3_000 });
  });

  // REGRESSION: postseason games must show O/U row
  test('Super Bowl game card shows Over/Under controls', async ({ page }) => {
    await expect(page.locator('[data-testid="over-under-controls"]').first()).toBeVisible({ timeout: 8_000 });
  });

  test('Alice sees her Super Bowl pick (SEA)', async ({ page }) => {
    await expect(page.getByRole('button', { name: 'Picked', exact: true })).toBeVisible({ timeout: 8_000 });
  });

  // REGRESSION: Wild Card must show all 6 games with O/U (not just 5)
  test('Wild Card shows 6 games all with Over/Under', async ({ page }) => {
    const weekSelect = page.getByRole('combobox').nth(1);
    await weekSelect.click();
    await page.getByRole('option', { name: 'Wild Card' }).click();
    await waitForSpinner(page);

    // Wait for all 6 games to load — wait for the last team in the list to appear
    await expect(page.getByText('HOU', { exact: true }).first()).toBeVisible({ timeout: 20_000 });
    // All 6 Wild Card games — backend normalizes JAX → JAC via NflTeamAbbrMapping
    for (const team of ['CAR', 'LAR', 'CHI', 'GB', 'JAC', 'BUF', 'PHI', 'SF', 'NE', 'LAC', 'PIT', 'HOU']) {
      await expect(page.getByText(team, { exact: true }).first()).toBeVisible({ timeout: 5_000 });
    }
    // All 6 games have O/U row
    await expect(page.locator('[data-testid="over-under-controls"]')).toHaveCount(6, { timeout: 10_000 });
  });

  test('Alice Wild Card picks are all visible', async ({ page }) => {
    const weekSelect = page.getByRole('combobox').nth(1);
    await weekSelect.click();
    await page.getByRole('option', { name: 'Wild Card' }).click();
    await waitForSpinner(page);
    // Alice picks all winners: LAR, CHI, BUF, SF, NE, HOU
    await expect(page.getByRole('button', { name: 'Picked', exact: true })).toHaveCount(6, { timeout: 8_000 });
  });

  // REGRESSION: regular season must show ALL 18 weeks (not just Week 1)
  test('regular season selector shows all 18 weeks', async ({ page }) => {
    const seasonTypeSelect = page.getByRole('combobox').last();
    await seasonTypeSelect.click();
    await page.getByRole('option', { name: 'Regular Season' }).click();
    await waitForSpinner(page);

    // WeekYearSelector defaults to last option (Week 18) after season-type switch
    await expect(page.getByRole('combobox').nth(1)).toContainText('Week 18', { timeout: 10_000 });

    // Open week dropdown and count options
    await page.getByRole('combobox').nth(1).click();
    await expect(page.getByRole('option', { name: 'Week 1', exact: true })).toBeVisible({ timeout: 5_000 });
    await expect(page.getByRole('option', { name: 'Week 18', exact: true })).toBeVisible({ timeout: 5_000 });
    // Should have 18 week options — count only items in the open listbox
    const weekOptions = page.locator('[role="listbox"] [role="option"]');
    await expect(weekOptions).toHaveCount(18, { timeout: 5_000 });
    await page.keyboard.press('Escape');
  });

  test('Week 18 regular season shows Alice\'s 4 picks', async ({ page }) => {
    const seasonTypeSelect = page.getByRole('combobox').last();
    await seasonTypeSelect.click();
    await page.getByRole('option', { name: 'Regular Season' }).click();
    await waitForSpinner(page);

    await expect(page.getByRole('button', { name: 'Picked', exact: true })).toHaveCount(4, { timeout: 8_000 });
  });

  test('"Current Week" button restores Super Bowl', async ({ page }) => {
    const seasonTypeSelect = page.getByRole('combobox').last();
    await seasonTypeSelect.click();
    await page.getByRole('option', { name: 'Regular Season' }).click();
    await waitForSpinner(page);

    await page.getByRole('button', { name: 'Current Week' }).click();
    await waitForSpinner(page);
    await expect(page.getByRole('combobox').nth(1)).toContainText('Super Bowl', { timeout: 5_000 });
    await expect(page.getByRole('button', { name: 'Current Week' })).not.toBeVisible();
  });

  test('no "Current Week" button when already on current week', async ({ page }) => {
    await expect(page.getByRole('button', { name: 'Current Week' })).not.toBeVisible();
  });
});
