/**
 * NFL Scores page — demo backend integration tests.
 * Runs against a live DEMO_MODE=true backend at localhost:5174.
 *
 * Week 18 (2025) has 4 final games seeded: DAL/LAR, GB/MIN, MIA/NE, WAS/PHI.
 * The frozen ESPN JSON also has in-progress and scheduled games.
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

  test('Week 8 games are displayed', async ({ page }) => {
    // BUF/TB is in the frozen ESPN data (in-progress at Q1)
    await expect(page.getByText('BUF').first()).toBeVisible({ timeout: 5_000 });
    // DAL/LAR is a final game
    await expect(page.getByText('DAL').first()).toBeVisible({ timeout: 5_000 });
  });

  test('league picks from demo users are shown', async ({ page }) => {
    // Multiple users picked BUF — the spread badge should show a count ≥ 1
    const bufBadge = page.locator('[data-testid="badge-BUF-spread"]');
    await expect(bufBadge).toBeVisible({ timeout: 8_000 });
    const badgeCount = bufBadge.locator('.MuiBadge-badge');
    await expect(badgeCount).toBeVisible();
    // Alice, Carlos, Eve all picked BUF → at least 3 (admin may also pick)
    const text = await badgeCount.textContent();
    expect(Number(text)).toBeGreaterThanOrEqual(3);
  });

  test('"Show only my picks" / "Show All Games" button is visible', async ({ page }) => {
    // The toggle is a Button that reads "Show Only My Picks" or "Show All Games"
    await expect(page.getByRole('button', { name: /show only my picks|show all games/i })).toBeVisible({ timeout: 5_000 });
  });

  test('week selector is visible', async ({ page }) => {
    const selects = page.getByRole('combobox');
    await expect(selects.first()).toBeVisible({ timeout: 5_000 });
  });
});
