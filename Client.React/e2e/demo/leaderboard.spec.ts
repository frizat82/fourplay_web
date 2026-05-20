/**
 * Leaderboard — demo backend integration tests.
 * Runs against a live DEMO_MODE=true backend at localhost:5174 (NFL league).
 *
 * All 5 demo users + admin are members of "Demo League".
 */
import { test, expect, type Page } from '@playwright/test';
import { waitForSpinner } from '../helpers/demoAuth';

// storageState (Alice logged in) is injected by the demo-nfl project in playwright.config.ts

async function gotoLeaderboard(page: Page): Promise<void> {
  // Land on /picks first so league hydrates, then client-side nav preserves session state
  await page.goto('/picks');
  await waitForSpinner(page);
  await page.getByRole('link', { name: 'Leaderboard' }).click();
  await page.waitForURL('**/leaderboard', { timeout: 8_000 });
  await waitForSpinner(page);
}

test.describe('Leaderboard — demo backend', () => {
  test('Leaderboard heading is visible', async ({ page }) => {
    await gotoLeaderboard(page);
    await expect(page.getByRole('heading', { name: 'Leaderboard' })).toBeVisible();
  });

  test('shows all 5 demo users in the standings', async ({ page }) => {
    await gotoLeaderboard(page);
    // At least Alice and Bob should appear in the table
    await expect(page.getByText('alice')).toBeVisible({ timeout: 5_000 });
    await expect(page.getByText('bob')).toBeVisible({ timeout: 5_000 });
    await expect(page.getByText('carlos')).toBeVisible({ timeout: 5_000 });
    await expect(page.getByText('dana')).toBeVisible({ timeout: 5_000 });
    await expect(page.getByText('eve')).toBeVisible({ timeout: 5_000 });
  });

  test('Rank and Total columns are visible', async ({ page }) => {
    await gotoLeaderboard(page);
    await expect(page.getByRole('columnheader', { name: 'Rank' })).toBeVisible({ timeout: 5_000 });
    await expect(page.getByRole('columnheader', { name: 'Total' })).toBeVisible({ timeout: 5_000 });
  });
});
