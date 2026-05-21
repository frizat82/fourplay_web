/**
 * NFL Scores page — demo backend integration tests.
 * Runs against a live DEMO_MODE=true backend at localhost:5174.
 *
 * Current "frozen" week: Wild Card (postseason week 1).
 * Wild Card real results: LAR 34-31 CAR, CHI 31-27 GB, BUF 27-24 JAX,
 *                         SF 23-19 PHI, NE 16-3 LAC, HOU 30-6 PIT.
 */
import { test, expect } from '@playwright/test';
import { waitForSpinner } from '../helpers/demoAuth';

// storageState (Alice logged in) is injected by the demo-nfl project in playwright.config.ts
test.describe('NFL scores — demo backend', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/scores');
    await waitForSpinner(page);
  });

  test('Scores heading is visible', async ({ page }) => {
    await expect(page.getByRole('heading', { name: 'Scores' })).toBeVisible();
  });

  // REGRESSION: current week should be Wild Card, not Week 18
  test('current week is Wild Card postseason', async ({ page }) => {
    await expect(page.getByRole('combobox').nth(1)).toContainText('Wild Card', { timeout: 8_000 });
    await expect(page.getByRole('combobox').last()).toContainText('Postseason', { timeout: 5_000 });
  });

  test('Wild Card games are displayed', async ({ page }) => {
    await expect(page.getByText('LAR').first()).toBeVisible({ timeout: 8_000 });
    await expect(page.getByText('BUF').first()).toBeVisible({ timeout: 5_000 });
    await expect(page.getByText('HOU').first()).toBeVisible({ timeout: 5_000 });
  });

  // REGRESSION: league picks badges must show for Wild Card (spreads seeded with real teams)
  test('Wild Card spread badges are visible', async ({ page }) => {
    // Alice picked LAR, GB, BUF, SF, LAC, HOU — badges should show for each
    const larBadge = page.locator('[data-testid="badge-LAR-spread"]');
    await expect(larBadge).toBeVisible({ timeout: 8_000 });
    const count = await larBadge.locator('.MuiBadge-badge').textContent();
    expect(Number(count)).toBeGreaterThanOrEqual(1);
  });

  // REGRESSION: spread badges must be green (cover) or red (no cover) for final games
  test('Wild Card badges show green/red cover colors for final games', async ({ page }) => {
    // LAR won 34-31 and covered (-6.5): badge should be success (green)
    const larBadge = page.locator('[data-testid="badge-LAR-spread"]');
    await expect(larBadge).toBeVisible({ timeout: 8_000 });
    // MUI Badge with color="success" gets class MuiBadge-colorSuccess
    const badge = larBadge.locator('.MuiBadge-badge');
    await expect(badge).toBeVisible();
    const classes = await badge.getAttribute('class');
    // Should have success or error color — NOT default (which means homeCovers=null)
    expect(classes).toMatch(/colorSuccess|colorError/);
  });

  test('"Show Only My Picks" button is visible', async ({ page }) => {
    await expect(page.getByRole('button', { name: /show only my picks|show all games/i })).toBeVisible({ timeout: 5_000 });
  });

  test('week selector is visible', async ({ page }) => {
    const selects = page.getByRole('combobox');
    await expect(selects.first()).toBeVisible({ timeout: 5_000 });
  });

  // REGRESSION: selecting Week 18 from dropdown must not flip back to in-progress when Current Week clicked
  test('selecting Week 18 shows all final games, Current Week returns to Wild Card', async ({ page }) => {
    const seasonTypeSelect = page.getByRole('combobox').last();
    await seasonTypeSelect.click();
    await page.getByRole('option', { name: 'Regular Season' }).click();
    await waitForSpinner(page);

    // Week 18 from real ESPN = all final
    await expect(page.getByRole('combobox').nth(1)).toContainText('Week 18', { timeout: 5_000 });

    // Click Current Week — goes back to Wild Card (postseason), not back to Week 18
    await page.getByRole('button', { name: 'Current Week' }).click();
    await waitForSpinner(page);
    await expect(page.getByRole('combobox').nth(1)).toContainText('Wild Card', { timeout: 5_000 });
    await expect(page.getByRole('combobox').last()).toContainText('Postseason', { timeout: 3_000 });
  });

  // REGRESSION: Super Bowl must not be empty
  test('Super Bowl is accessible and shows a game', async ({ page }) => {
    const weekSelect = page.getByRole('combobox').nth(1);
    await weekSelect.click();
    const sbOption = page.getByRole('option', { name: 'Super Bowl' });
    await expect(sbOption).toBeVisible({ timeout: 5_000 });
    await sbOption.click();
    await waitForSpinner(page);

    await expect(page.getByText('NE').first()).toBeVisible({ timeout: 8_000 });
    await expect(page.getByText('SEA').first()).toBeVisible({ timeout: 5_000 });
    await expect(page.getByText('No Odds Available')).not.toBeVisible({ timeout: 3_000 });
  });
});
