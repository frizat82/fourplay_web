import { test, expect } from '@playwright/test';
import { mockAuth } from './helpers/auth';
import { createPick } from '../src/test/fixtures';

/**
 * Scores page → "Show As Matrix": who is still alive this week.
 *
 * Mocked games (both final): BUF 21–MIA 14 with BUF −3.5 / MIA +3.5, DAL 28–NYG 17 with DAL −6.5.
 * Week 21 (postseason) requires 2 picks. Zoe (BUF, DAL) covers both; Aaron (MIA, DAL) loses MIA.
 */
test.describe('Scores matrix — survivors', () => {
  const leaguePicks = [
    createPick({ team: 'MIA', pick: 'Spread', userId: 'u1', userName: 'Aaron' }),
    createPick({ team: 'DAL', pick: 'Spread', userId: 'u1', userName: 'Aaron' }),
    createPick({ team: 'BUF', pick: 'Spread', userId: 'u2', userName: 'Zoe' }),
    createPick({ team: 'DAL', pick: 'Spread', userId: 'u2', userName: 'Zoe' }),
  ];
  const options = { leaguePicks, gameStarted: true, isPostSeason: true, week: 21 };

  test('lists the member still alive above the member who is out', async ({ page }) => {
    await mockAuth(page, { navigateTo: '/scores', ...options });
    await page.getByRole('button', { name: 'Show As Matrix' }).click();

    await expect(page.getByRole('img', { name: 'Zoe: won, all picks covered' })).toBeVisible();
    await expect(page.getByRole('img', { name: 'Aaron: out' })).toBeVisible();
    await expect(page.getByLabel("MIA: didn't cover")).toBeVisible();
    await expect(page.getByLabel('BUF: covered')).toBeVisible();

    const rows = page.locator('tbody tr');
    await expect(rows.nth(0)).toContainText('Zoe');
    await expect(rows.nth(1)).toContainText('Aaron');
    await expect(page.getByRole('group', { name: 'Picks' }).getByText('Covering (live)', { exact: true })).toBeVisible();
  });

  test('offers no matrix when nobody has a revealed pick', async ({ page }) => {
    await mockAuth(page, { navigateTo: '/scores', ...options, leaguePicks: [] });
    await expect(page.getByRole('heading', { name: 'Scores' })).toBeVisible({ timeout: 10000 });
    await expect(page.getByRole('button', { name: 'Show As Matrix' })).toHaveCount(0);
  });
});
