/**
 * CFB Scores page — demo backend integration tests.
 * Runs against a live DEMO_MODE=true backend at cfb.localhost:5174.
 *
 * Championship game: IU 14, MIA 7 (in-progress Q3).
 * IU covers (-3 spread: IU+(-3)=11 > 7), MIA does not.
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

  // Championship is in-progress (Q3: IU 14, MIA 7) so field position renders
  test('Championship shows game clock (Q3) indicator and partial scores', async ({ page }) => {
    // Shows "Q3 7:23" from CFB_DEMO_SITUATION, or "Live" as fallback
    const clockText = page.getByText(/Q[1-4]|Live/);
    await expect(clockText.first()).toBeVisible({ timeout: 8_000 });
    await expect(page.getByRole('heading', { name: '14' })).toBeVisible({ timeout: 5_000 });
    await expect(page.getByRole('heading', { name: '7' })).toBeVisible({ timeout: 5_000 });
  });

  test('Championship shows field position bar (in-progress)', async ({ page }) => {
    await expect(page.locator('[data-testid="field-position-bar"]')).toBeVisible({ timeout: 8_000 });
    await expect(page.locator('[data-testid="ball-marker"]')).toBeVisible({ timeout: 5_000 });
  });

  test('Championship shows down and distance text', async ({ page }) => {
    await expect(page.getByText('3rd & 4 at MIA 22')).toBeVisible({ timeout: 8_000 });
  });

  test('league picks badge is visible for the Championship game', async ({ page }) => {
    // IU spread badge: Alice, Carlos, Eve picked IU → at least 3 picks
    const iuBadge = page.locator('[data-testid="badge-IU-spread"]');
    await expect(iuBadge).toBeVisible({ timeout: 8_000 });
    const count = await iuBadge.locator('.MuiBadge-badge').textContent();
    expect(Number(count)).toBeGreaterThanOrEqual(3);
  });

  // REGRESSION: in-progress CFB game must show green/red cover badges (same as NFL)
  // IU covers: 14 + (-3) = 11 > 7 → IU badge = green, MIA badge = red
  test('Championship badges show green/red cover colors (in-progress)', async ({ page }) => {
    const successBadges = page.locator('.MuiBadge-colorSuccess:not(.MuiBadge-invisible)');
    const errorBadges = page.locator('.MuiBadge-colorError:not(.MuiBadge-invisible)');
    await expect(successBadges.first()).toBeVisible({ timeout: 8_000 });
    await expect(errorBadges.first()).toBeVisible({ timeout: 5_000 });
  });

  // REGRESSION: switching to regular season must show Week 13 (18-slate system; same race-condition fix as NFL)
  test('regular season selector shows Week 13 by default after switch', async ({ page }) => {
    const seasonTypeSelect = page.getByRole('combobox').last();
    await seasonTypeSelect.click();
    await page.getByRole('option', { name: 'Regular Season' }).click();
    await expect(page.getByRole('combobox').nth(1)).toContainText('Week 13', { timeout: 8_000 });
  });

  test('week selector is visible', async ({ page }) => {
    const selects = page.getByRole('combobox');
    await expect(selects.first()).toBeVisible({ timeout: 5_000 });
  });
});
