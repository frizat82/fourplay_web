import { test, expect } from '@playwright/test';
import { mockAuth, waitForSpinner, TEST_USER } from './helpers/auth';
import { createPick } from '../src/test/fixtures';
import type { LeaderboardDto, WeekResult } from '../src/types/leaderboard';

/**
 * Dashboard week-win celebration. The mocked week (2) is final: BUF 21–MIA 14 with BUF −3.5,
 * DAL 28–NYG 17 with DAL −6.5. TEST_USER picked BUF and DAL.
 */
test.describe('Week win celebration', () => {
  const board = (mine: WeekResult, score: number): LeaderboardDto[] => {
    const row = (userId: string, userName: string, weekResult: WeekResult, s: number): LeaderboardDto => ({
      userId, userName, rank: '1', total: s,
      weekResults: [
        { week: 1, weekResult: 'Lost', score: 0, losingTeams: [], hadScoringError: false },
        { week: 2, weekResult, score: s, losingTeams: [], hadScoringError: false },
      ],
    });
    return [row(TEST_USER.userId, TEST_USER.name, mine, score), row('rival-1', 'Rival', mine === 'Won' ? 'Lost' : 'Won', -score)];
  };
  const userPicks = [
    createPick({ team: 'BUF', pick: 'Spread', userId: TEST_USER.userId, userName: TEST_USER.name }),
    createPick({ team: 'DAL', pick: 'Spread', userId: TEST_USER.userId, userName: TEST_USER.name }),
  ];
  const options = { authUser: TEST_USER, navigateTo: '/dashboard', userPicks, leaguePicks: userPicks, gameStarted: true };

  test('plays once for a winner: a tap closes it and it stays closed after a reload', async ({ page }) => {
    await mockAuth(page, { ...options, leaderboard: board('Won', 25) });
    await waitForSpinner(page);

    const dialog = page.getByRole('dialog', { name: 'You won the week!' });
    await expect(dialog).toBeVisible({ timeout: 10_000 });
    await expect(dialog).toContainText('Test League');
    await expect(dialog.getByLabel('You collect $25')).toHaveText('+$25');
    await expect(dialog.getByRole('img', { name: 'BUF covered by 3.5' })).toBeVisible();
    await expect(dialog.getByRole('img', { name: 'DAL covered by 4.5' })).toBeVisible();

    await dialog.getByText('You won the week!').click();
    await expect(dialog).toBeHidden();

    await page.reload();
    await expect(page.getByTestId('dashboard-standings').getByText('+25')).toBeVisible({ timeout: 10_000 });
    await expect(dialog).toBeHidden();
  });

  test('does not play for a member who lost the week', async ({ page }) => {
    await mockAuth(page, { ...options, leaderboard: board('Lost', -25) });
    await waitForSpinner(page);

    await expect(page.getByTestId('dashboard-standings').getByText('-25')).toBeVisible({ timeout: 10_000 });
    await expect(page.getByRole('dialog')).toHaveCount(0);
  });
});
