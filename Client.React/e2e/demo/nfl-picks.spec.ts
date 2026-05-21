/**
 * NFL Picks page — demo backend integration tests.
 * Runs against a live DEMO_MODE=true backend at localhost:5174.
 *
 * Alice's Week 18 (2025) picks: BUF, DAL, MIN, MIA (all Spread).
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

  test('Alice sees her 4 existing Week 8 picks', async ({ page }) => {
    // All 4 of Alice's picks (BUF, DAL, MIN, MIA) should show as "Picked"
    const pickedButtons = page.getByRole('button', { name: 'Picked', exact: true });
    await expect(pickedButtons).toHaveCount(4, { timeout: 8_000 });
  });

  test('Week 8 games are visible (BUF/TB and DAL/LAR among them)', async ({ page }) => {
    await expect(page.getByText('BUF')).toBeVisible({ timeout: 5_000 });
    await expect(page.getByText('DAL')).toBeVisible({ timeout: 5_000 });
  });

  test('week selector is visible', async ({ page }) => {
    // The WeekYearSelector renders three MUI Select dropdowns
    const selects = page.getByRole('combobox');
    await expect(selects.first()).toBeVisible({ timeout: 5_000 });
  });

  test('no "Current Week" button on current week', async ({ page }) => {
    // "Current Week" button only appears when browsing historical weeks
    await expect(page.getByRole('button', { name: 'Current Week' })).not.toBeVisible();
  });

  test('navigating to a different week shows inline "No Odds Available" — not a countdown', async ({ page }) => {
    // Click "Previous" to move away from current week
    const prevButton = page.getByRole('button', { name: 'Previous' });
    await expect(prevButton).toBeVisible({ timeout: 5_000 });
    await prevButton.click();
    await waitForSpinner(page);

    // "Current Week" button should now appear
    await expect(page.getByRole('button', { name: 'Current Week' })).toBeVisible({ timeout: 5_000 });

    // If the prior week has no odds it shows "No Odds Available" — never a countdown
    // The countdown text contains "Next spread release" — assert it's absent
    await expect(page.getByText(/next spread release/i)).not.toBeVisible({ timeout: 3_000 });
  });

  test('"Current Week" button restores current week', async ({ page }) => {
    // Navigate away first
    await page.getByRole('button', { name: 'Previous' }).click();
    await waitForSpinner(page);
    const currentWeekBtn = page.getByRole('button', { name: 'Current Week' });
    await expect(currentWeekBtn).toBeVisible({ timeout: 5_000 });

    // Click it — should reload current week and hide the button
    await currentWeekBtn.click();
    await waitForSpinner(page);
    await expect(page.getByRole('button', { name: 'Current Week' })).not.toBeVisible({ timeout: 5_000 });
    // Picks should still show (Alice's 4 picks are still in Week 8)
    await expect(page.getByRole('button', { name: 'Picked', exact: true })).toHaveCount(4, { timeout: 8_000 });
  });
});
