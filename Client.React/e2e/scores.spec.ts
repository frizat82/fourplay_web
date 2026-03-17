import { test, expect } from '@playwright/test';
import { mockAuth } from './helpers/auth';
import { createPick } from '../src/test/fixtures';

/**
 * Scores page e2e tests.
 *
 * Badge visibility requires games to have started (status_final) so that
 * shouldShowGamePicks() returns true, which makes getUserPicksCount() > 0
 * and the badge becomes visible. We use gameStarted: true for this suite.
 *
 * Over/Under badges only render during postseason games, so those tests
 * use isPostSeason: true.
 */
test.describe('Scores page (authenticated)', () => {
  /**
   * League picks for the two games:
   *   BUF: 2 Spread picks (Alice, Bob)
   *   MIA: 1 Spread pick  (Carol)
   *   BUF: 1 Over pick     (Dave)
   *   MIA: 1 Under pick    (Eve)
   */
  const leaguePicks = [
    createPick({ team: 'BUF', pick: 'Spread', userId: 'u1', userName: 'Alice' }),
    createPick({ team: 'BUF', pick: 'Spread', userId: 'u2', userName: 'Bob' }),
    createPick({ team: 'MIA', pick: 'Spread', userId: 'u3', userName: 'Carol' }),
    createPick({ team: 'BUF', pick: 'Over',   userId: 'u4', userName: 'Dave' }),
    createPick({ team: 'BUF', pick: 'Under',  userId: 'u5', userName: 'Eve' }),
  ];

  // gameStarted: true so shouldShowGamePicks() = true and badge counts are visible
  const scoresOptions = { leaguePicks, gameStarted: true };
  // postseason variant for Over/Under badge tests
  const postSeasonOptions = { leaguePicks, gameStarted: true, isPostSeason: true };

  test('renders scores page for authenticated user', async ({ page }) => {
    await mockAuth(page, { navigateTo: '/scores', ...scoresOptions });

    // Wait for loading spinner to clear
    await expect(page.getByRole('progressbar')).not.toBeVisible({ timeout: 10000 });

    // Page header should show "Scores"
    await expect(page.getByRole('heading', { name: 'Scores' })).toBeVisible({ timeout: 5000 });
  });

  test('shows spread badge count for BUF (2 picks)', async ({ page }) => {
    await mockAuth(page, { navigateTo: '/scores', ...scoresOptions });

    await expect(page.getByRole('progressbar')).not.toBeVisible({ timeout: 10000 });

    // The badge wrapper has data-testid="badge-BUF-spread"
    // MUI Badge renders the count in a child <span class="MuiBadge-badge">
    const bufBadge = page.locator('[data-testid="badge-BUF-spread"]');
    await expect(bufBadge).toBeVisible({ timeout: 5000 });
    await expect(bufBadge.locator('.MuiBadge-badge')).toHaveText('2', { timeout: 5000 });
  });

  test('shows spread badge count for MIA (1 pick)', async ({ page }) => {
    await mockAuth(page, { navigateTo: '/scores', ...scoresOptions });

    await expect(page.getByRole('progressbar')).not.toBeVisible({ timeout: 10000 });

    const miaBadge = page.locator('[data-testid="badge-MIA-spread"]');
    await expect(miaBadge).toBeVisible({ timeout: 5000 });
    await expect(miaBadge.locator('.MuiBadge-badge')).toHaveText('1', { timeout: 5000 });
  });

  test('shows over badge count (1 pick) during postseason', async ({ page }) => {
    await mockAuth(page, { navigateTo: '/scores', ...postSeasonOptions });

    await expect(page.getByRole('progressbar')).not.toBeVisible({ timeout: 10000 });

    const overBadge = page.locator('[data-testid="badge-BUF-over"]');
    await expect(overBadge).toBeVisible({ timeout: 5000 });
    await expect(overBadge.locator('.MuiBadge-badge')).toHaveText('1', { timeout: 5000 });
  });

  test('shows under badge count (1 pick) during postseason', async ({ page }) => {
    await mockAuth(page, { navigateTo: '/scores', ...postSeasonOptions });

    await expect(page.getByRole('progressbar')).not.toBeVisible({ timeout: 10000 });

    const underBadge = page.locator('[data-testid="badge-BUF-under"]');
    await expect(underBadge).toBeVisible({ timeout: 5000 });
    await expect(underBadge.locator('.MuiBadge-badge')).toHaveText('1', { timeout: 5000 });
  });
});
