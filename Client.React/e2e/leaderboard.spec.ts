import { test, expect, type Page } from '@playwright/test';
import { mockAuth, TEST_USER, waitForSpinner } from './helpers/auth';
import type { LeaderboardDto } from '../src/types/leaderboard';
import { createLeaderboardEntry } from '../src/test/fixtures';

const sampleLeaderboard: LeaderboardDto[] = [
  createLeaderboardEntry({
    userId: TEST_USER.userId,
    userName: TEST_USER.name,
    rank: '1',
    total: 25,
    weekResults: [
      { week: 1, weekResult: 'Won', score: 15 },
      { week: 2, weekResult: 'Lost', score: 10 },
    ],
  }),
  createLeaderboardEntry({
    userId: 'u2',
    userName: 'Alice',
    rank: '2',
    total: 18,
    weekResults: [
      { week: 1, weekResult: 'Lost', score: 8 },
      { week: 2, weekResult: 'Won', score: 10 },
    ],
  }),
];

/**
 * The LeaderboardPage redirects to /leaguepicker if currentLeague is null.
 * SessionProvider loads currentLeague asynchronously. A full page.goto('/leaderboard')
 * resets React state so the session hasn't loaded yet when LeaderboardPage mounts.
 * Fix: land on /picks first (which tolerates null league), wait for it to load so
 * the session settles, then do a client-side nav via the sidebar link — that
 * preserves SessionProvider state so currentLeague=1 is already set.
 */
async function gotoLeaderboard(page: Page, leaderboard: LeaderboardDto[]) {
  // Land on /picks — session hydrates here (auth + league API calls complete)
  await mockAuth(page, { navigateTo: '/picks', leaderboard });
  await waitForSpinner(page);
  // Client-side nav preserves React state (no page reload, currentLeague stays set)
  await page.getByRole('link', { name: 'Leaderboard' }).click();
  await page.waitForURL('**/leaderboard', { timeout: 5000 });
  // Wait for leaderboard loading spinner to clear before returning
  await waitForSpinner(page);
}

test.describe('Leaderboard page (authenticated)', () => {
  test('renders leaderboard heading', async ({ page }) => {
    await gotoLeaderboard(page, sampleLeaderboard);
    await expect(page.getByRole('heading', { name: 'Leaderboard' })).toBeVisible({ timeout: 5000 });
  });

  test('shows standings table with user rows', async ({ page }) => {
    await gotoLeaderboard(page, sampleLeaderboard);
    await expect(page.getByText(TEST_USER.name)).toBeVisible({ timeout: 5000 });
    await expect(page.getByText('Alice')).toBeVisible({ timeout: 5000 });
  });

  test('shows rank and total columns', async ({ page }) => {
    await gotoLeaderboard(page, sampleLeaderboard);
    await expect(page.getByRole('columnheader', { name: 'Rank' })).toBeVisible({ timeout: 5000 });
    await expect(page.getByRole('columnheader', { name: 'User' })).toBeVisible({ timeout: 5000 });
    await expect(page.getByRole('columnheader', { name: 'Total' })).toBeVisible({ timeout: 5000 });
  });

  test('shows empty state (no Standings table) when leaderboard is empty', async ({ page }) => {
    await gotoLeaderboard(page, []);
    await expect(page.getByRole('heading', { name: 'Leaderboard' })).toBeVisible({ timeout: 5000 });
    await expect(page.getByText('Standings')).not.toBeVisible();
  });
});
