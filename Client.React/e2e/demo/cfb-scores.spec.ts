/**
 * CFB Scores page — demo backend integration tests.
 * Runs against a live DEMO_MODE=true backend at cfb.localhost:5174.
 *
 * Championship game: IU 23, MIA 20 (final).
 * All 5 demo users have picks for this slate.
 */
import { test, expect } from '@playwright/test';
import { waitForSpinner } from '../helpers/demoAuth';

// baseURL (cfb.localhost:5174) and storageState (Alice logged in) are injected
// by the demo-cfb project in playwright.config.ts.
test.describe('CFB scores — demo backend', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/scores');
    await waitForSpinner(page);
  });

  test('Scores heading is visible', async ({ page }) => {
    await expect(page.getByRole('heading', { name: 'Scores' })).toBeVisible();
  });

  test('CFP Championship game is displayed (IU vs MIA)', async ({ page }) => {
    await expect(page.getByText('IU').first()).toBeVisible({ timeout: 5_000 });
    await expect(page.getByText('MIA').first()).toBeVisible({ timeout: 5_000 });
  });

  test('Championship final score is shown (IU 23, MIA 20)', async ({ page }) => {
    // Use heading role — scores render as h6 elements
    await expect(page.getByRole('heading', { name: '23' })).toBeVisible({ timeout: 5_000 });
    await expect(page.getByRole('heading', { name: '20' })).toBeVisible({ timeout: 5_000 });
  });

  test('league picks badge is visible for the Championship game', async ({ page }) => {
    // IU spread badge: Alice, Carlos, Eve picked IU → at least 3 picks
    const iuBadge = page.locator('[data-testid="badge-IU-spread"]');
    await expect(iuBadge).toBeVisible({ timeout: 8_000 });
    const count = await iuBadge.locator('.MuiBadge-badge').textContent();
    expect(Number(count)).toBeGreaterThanOrEqual(3);
  });

  test('week selector is visible', async ({ page }) => {
    const selects = page.getByRole('combobox');
    await expect(selects.first()).toBeVisible({ timeout: 5_000 });
  });
});
