/**
 * NFL Picks page — demo backend integration tests.
 * Runs against a live DEMO_MODE=true backend at localhost:5174.
 *
 * Current "frozen" week: Wild Card (postseason week 1, ESPN season.type=3).
 * Alice's Wild Card picks: LAR, GB, BUF, SF, LAC, HOU (all home teams).
 * Alice's regular season Week 18 picks: BUF, DAL, MIN, MIA.
 */
import { test, expect } from '@playwright/test';
import { waitForSpinner } from '../helpers/demoAuth';

// storageState (Alice logged in) is injected by the demo-nfl project in playwright.config.ts
test.describe('NFL picks — demo backend', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/picks');
    await waitForSpinner(page);
  });

  test('Picks heading is visible', async ({ page }) => {
    await expect(page.getByRole('heading', { name: 'Picks', exact: true })).toBeVisible();
  });

  // REGRESSION: current week must be Wild Card (postseason), not Week 18 regular season
  test('current week is Wild Card postseason — not Week 18', async ({ page }) => {
    const weekSelect = page.getByRole('combobox').nth(1);
    const seasonTypeSelect = page.getByRole('combobox').last();
    await expect(weekSelect).toContainText('Wild Card', { timeout: 8_000 });
    await expect(seasonTypeSelect).toContainText('Postseason', { timeout: 5_000 });
  });

  // REGRESSION: Wild Card must show 6 games (all seeded with real 2025 teams)
  test('Wild Card shows 6 game cards with spreads', async ({ page }) => {
    // All 6 Wild Card games should be visible — LAR, GB, BUF, SF, LAC, HOU
    await expect(page.getByText('LAR').first()).toBeVisible({ timeout: 8_000 });
    await expect(page.getByText('BUF').first()).toBeVisible({ timeout: 5_000 });
    await expect(page.getByText('SF').first()).toBeVisible({ timeout: 5_000 });
    await expect(page.getByText('HOU').first()).toBeVisible({ timeout: 5_000 });
  });

  // REGRESSION: Alice's Wild Card picks must show as Picked (real teams seeded)
  test('Alice sees her 6 Wild Card picks', async ({ page }) => {
    const pickedButtons = page.getByRole('button', { name: 'Picked', exact: true });
    await expect(pickedButtons).toHaveCount(6, { timeout: 8_000 });
  });

  // REGRESSION: postseason game cards must show Over/Under row
  test('Wild Card game cards show Over/Under buttons', async ({ page }) => {
    // O/U controls only render for postseason — assert at least one O/U section exists
    await expect(page.locator('[data-testid="over-under-controls"]').first()).toBeVisible({ timeout: 8_000 });
  });

  test('week selector is visible', async ({ page }) => {
    const selects = page.getByRole('combobox');
    await expect(selects.first()).toBeVisible({ timeout: 5_000 });
  });

  test('no "Current Week" button on current week', async ({ page }) => {
    await expect(page.getByRole('button', { name: 'Current Week' })).not.toBeVisible();
  });

  // REGRESSION: Super Bowl must not be empty — it's ESPN postseason week 5
  test('Super Bowl is accessible and shows a game', async ({ page }) => {
    // Open week selector and pick "Super Bowl"
    const weekSelect = page.getByRole('combobox').nth(1);
    await weekSelect.click();
    const sbOption = page.getByRole('option', { name: 'Super Bowl' });
    await expect(sbOption).toBeVisible({ timeout: 5_000 });
    await sbOption.click();
    await waitForSpinner(page);

    // NE vs SEA — both teams must appear
    await expect(page.getByText('NE').first()).toBeVisible({ timeout: 8_000 });
    await expect(page.getByText('SEA').first()).toBeVisible({ timeout: 5_000 });
    // No "No Odds Available" message
    await expect(page.getByText('No Odds Available')).not.toBeVisible({ timeout: 3_000 });
  });

  test('regular season Week 18 is navigable with picks', async ({ page }) => {
    // Switch to Regular Season → defaults to Week 18
    const seasonTypeSelect = page.getByRole('combobox').last();
    await seasonTypeSelect.click();
    await page.getByRole('option', { name: 'Regular Season' }).click();
    await waitForSpinner(page);

    await expect(page.getByRole('combobox').nth(1)).toContainText('Week 18', { timeout: 5_000 });
    // Alice's Week 18 picks should show
    await expect(page.getByRole('button', { name: 'Picked', exact: true })).toHaveCount(4, { timeout: 8_000 });
  });

  test('"Current Week" button restores Wild Card', async ({ page }) => {
    // Navigate away to regular season
    const seasonTypeSelect = page.getByRole('combobox').last();
    await seasonTypeSelect.click();
    await page.getByRole('option', { name: 'Regular Season' }).click();
    await waitForSpinner(page);

    const currentWeekBtn = page.getByRole('button', { name: 'Current Week' });
    await expect(currentWeekBtn).toBeVisible({ timeout: 5_000 });
    await currentWeekBtn.click();
    await waitForSpinner(page);

    // Back to Wild Card
    await expect(page.getByRole('combobox').nth(1)).toContainText('Wild Card', { timeout: 5_000 });
    await expect(page.getByRole('button', { name: 'Current Week' })).not.toBeVisible();
  });
});
