import { test, expect } from '@playwright/test';
import { mockCfbAuth, createCfbPick } from './helpers/cfbRoutes';

// CFB counterpart to scores.spec.ts. Runs under a cfb.localhost baseURL so
// useSportContext resolves the CFB adapter (see cfbRoutes.ts's mockCfbAuth).
// gameStarted: true so shouldShowGamePicks() = true and badge counts are visible —
// same reasoning as scores.spec.ts.
test.use({ baseURL: 'http://cfb.localhost:5173' });

test.describe('CFB Scores page (authenticated)', () => {
  /**
   * League picks for the two games:
   *   OSU: 2 Spread picks (Alice, Bob)
   *   ALA: 1 Spread pick  (Carol)
   */
  const leaguePicks = [
    createCfbPick({ team: 'OSU', pickType: 'Spread', userId: 'u1', userName: 'Alice' }),
    createCfbPick({ team: 'OSU', pickType: 'Spread', userId: 'u2', userName: 'Bob' }),
    createCfbPick({ team: 'ALA', pickType: 'Spread', userId: 'u3', userName: 'Carol' }),
  ];

  const scoresOptions = { leaguePicks, gameStarted: true };

  test('renders scores page for authenticated user', async ({ page }) => {
    await mockCfbAuth(page, { navigateTo: '/scores', ...scoresOptions });

    await expect(page.getByRole('progressbar')).not.toBeVisible({ timeout: 10000 });
    await expect(page.getByRole('heading', { name: 'Scores' })).toBeVisible({ timeout: 5000 });
  });

  test('shows spread badge count for OSU (2 picks)', async ({ page }) => {
    await mockCfbAuth(page, { navigateTo: '/scores', ...scoresOptions });

    await expect(page.getByRole('progressbar')).not.toBeVisible({ timeout: 10000 });

    const osuBadge = page.locator('[data-testid="badge-OSU-spread"]');
    await expect(osuBadge).toBeVisible({ timeout: 5000 });
    await expect(osuBadge.locator('.MuiBadge-badge')).toHaveText('2', { timeout: 5000 });
  });

  test('shows spread badge count for ALA (1 pick)', async ({ page }) => {
    await mockCfbAuth(page, { navigateTo: '/scores', ...scoresOptions });

    await expect(page.getByRole('progressbar')).not.toBeVisible({ timeout: 10000 });

    const alaBadge = page.locator('[data-testid="badge-ALA-spread"]');
    await expect(alaBadge).toBeVisible({ timeout: 5000 });
    await expect(alaBadge.locator('.MuiBadge-badge')).toHaveText('1', { timeout: 5000 });
  });

  // OSU is seeded ranked #3 (setupCfbRoutes's cfbSpread fixture); MICH/ALA/UGA are unranked.
  test('shows AP rank next to a ranked team, and no rank badge for unranked teams', async ({ page }) => {
    await mockCfbAuth(page, { navigateTo: '/scores', ...scoresOptions });

    await expect(page.getByRole('progressbar')).not.toBeVisible({ timeout: 10000 });
    await expect(page.getByText('#3')).toBeVisible({ timeout: 5000 });
    await expect(page.getByText(/^#(?!3)\d+$/)).toHaveCount(0);
  });
});
