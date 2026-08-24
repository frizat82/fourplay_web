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
    await expect(page.getByRole('cell', { name: TEST_USER.name })).toBeVisible({ timeout: 5000 });
    await expect(page.getByRole('cell', { name: 'Alice' })).toBeVisible({ timeout: 5000 });
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

  test('season selector defaults to the current season, and switching seasons re-fetches standings for that year', async ({ page }) => {
    await gotoLeaderboard(page, sampleLeaderboard);
    await expect(page.getByText('2024 Season')).toBeVisible({ timeout: 5000 }); // TEST_SEASON in e2e/helpers/routes.ts

    // Registered after the default mock (from setupRoutes, via gotoLeaderboard/mockAuth) so it
    // takes over for any leaderboard request from here on — proves the season change actually
    // fires a new request for the newly-selected year, not just a client-side re-render.
    const requestedSeasons: string[] = [];
    await page.route(/\/api\/leaderboard\/\d+\/leaderboard\/\d+/, (route) => {
      requestedSeasons.push(new URL(route.request().url()).pathname.split('/').pop()!);
      void route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(sampleLeaderboard) });
    });

    // Other combobox-role selectors (e.g. Picks' week/season-type selector) can linger in the
    // DOM after the client-side nav gotoLeaderboard does — target this one by its visible text.
    await page.getByText('2024 Season').click();
    await page.getByRole('option', { name: '2022 Season' }).click();

    await expect(page.getByRole('combobox').filter({ hasText: '2022 Season' })).toBeVisible({ timeout: 5000 });
    // Not asserting the exact call list — an incidental extra same-season refetch is possible
    // depending on render timing and isn't what this test is about — just that switching to
    // 2022 actually fires a real request for that year, not just a client-side re-render.
    expect(requestedSeasons.at(-1)).toBe('2022');
  });

  test('Share button renders the standings card and shares it (falls back to download when the browser cannot share files)', async ({ page }) => {
    // Force the download-fallback branch deterministically, rather than relying on whatever
    // Web Share API support this Chromium build happens to have.
    await page.addInitScript(() => {
      Object.defineProperty(window.navigator, 'canShare', { value: () => false, configurable: true });
    });

    await gotoLeaderboard(page, sampleLeaderboard);
    await page.getByRole('button', { name: /^share$/i }).click();

    await expect(page.getByRole('alert')).toContainText(/downloaded/i, { timeout: 5000 });
  });
});
