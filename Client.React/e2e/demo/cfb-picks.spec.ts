/**
 * CFB Picks page — demo backend integration tests.
 * Runs against a live DEMO_MODE=true backend at cfb.localhost:5174.
 *
 * Alice's CFP Championship pick: IU (Indiana).
 * Alice's CFB Week 8 picks: all 6 home teams (MICH, ALA, OSU, UGA, LSU, CLEM).
 *
 * Regression guards:
 *  - CFB regular season selector must have 14 weeks (not 5 — old bug)
 *  - Navigating to regular season shows Week 14 by default (most recent)
 *  - Week 8 is reachable from the selector
 */
import { test, expect } from '@playwright/test';
import { waitForSpinner } from '../helpers/demoAuth';

// baseURL (cfb.localhost:5174) and storageState (Alice logged in) are injected
// by the demo-cfb project in playwright.config.ts — no per-file overrides needed.
test.describe('CFB picks — demo backend', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/picks');
    await waitForSpinner(page);
  });

  test('Picks heading is visible', async ({ page }) => {
    await expect(page.getByRole('heading', { name: 'Picks', exact: true })).toBeVisible();
  });

  test('defaults to CFP Championship (latest past slate)', async ({ page }) => {
    // The active slate is the CFP Championship (SlateNumber 19, latest by end date)
    await expect(page.getByText('CFP Championship')).toBeVisible({ timeout: 5_000 });
  });

  test('Alice sees her CFP Championship pick (IU)', async ({ page }) => {
    // Alice picked IU (Indiana) for the Championship
    // The "Picked" button for IU should be visible
    await expect(page.getByRole('button', { name: 'Picked', exact: true })).toBeVisible({ timeout: 8_000 });
    // IU team abbreviation should appear in a Picked row
    await expect(page.getByText('IU')).toBeVisible({ timeout: 5_000 });
  });

  test('CFP Championship game card shows IU vs MIA', async ({ page }) => {
    await expect(page.getByText('IU')).toBeVisible({ timeout: 5_000 });
    await expect(page.getByText('MIA')).toBeVisible({ timeout: 5_000 });
  });

  test('switching to Regular Season shows Week 14 (most recent)', async ({ page }) => {
    // Click the season-type select and pick "Regular Season"
    const seasonTypeSelect = page.getByRole('combobox').last();
    await seasonTypeSelect.click();
    await page.getByRole('option', { name: 'Regular Season' }).click();
    await waitForSpinner(page);

    // Should default to the LAST regular season week (Week 14)
    const weekSelect = page.getByRole('combobox').nth(1);
    await expect(weekSelect).toContainText('Week 14', { timeout: 5_000 });
  });

  // REGRESSION GUARD: Was broken — selector was showing only 5 weeks for regular season
  test('CFB regular season selector has 14 weeks', async ({ page }) => {
    // Switch to Regular Season
    const seasonTypeSelect = page.getByRole('combobox').last();
    await seasonTypeSelect.click();
    await page.getByRole('option', { name: 'Regular Season' }).click();
    await waitForSpinner(page);

    // Open the week select dropdown
    const weekSelect = page.getByRole('combobox').nth(1);
    await weekSelect.click();
    // Count Week options — must be 14
    const weekOptions = page.getByRole('option', { name: /^Week \d+$/ });
    await expect(weekOptions).toHaveCount(14, { timeout: 5_000 });
    // Close dropdown
    await page.keyboard.press('Escape');
  });

  test('CFB Week 8 is accessible and shows 6 game cards', async ({ page }) => {
    // Switch to Regular Season
    const seasonTypeSelect = page.getByRole('combobox').last();
    await seasonTypeSelect.click();
    await page.getByRole('option', { name: 'Regular Season' }).click();
    await waitForSpinner(page);

    // Navigate to Week 8 via the week dropdown
    const weekSelect = page.getByRole('combobox').nth(1);
    await weekSelect.click();
    await page.getByRole('option', { name: 'Week 8' }).click();
    await waitForSpinner(page);

    // 6 games were seeded for Week 8 — each game card shows two team abbreviations
    await expect(page.getByText('MICH')).toBeVisible({ timeout: 5_000 });
    await expect(page.getByText('PSU')).toBeVisible({ timeout: 5_000 });
    // Alice picked all favorites — should see 6 "Picked" buttons
    await expect(page.getByRole('button', { name: 'Picked', exact: true })).toHaveCount(6, { timeout: 8_000 });
  });
});
