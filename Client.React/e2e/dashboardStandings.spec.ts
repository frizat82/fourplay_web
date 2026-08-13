import { test, expect } from '@playwright/test';
import { mockAuth, waitForSpinner, TEST_USER } from './helpers/auth';

// TEST_USER is wired in routes.ts to belong to "Test League" (NFL, id TEST_LEAGUE_ID) via
// /api/league/user-mappings/by-user/ — that's what DashboardStandings' useSession().availableLeagues
// resolves to here.
test.describe('Dashboard standings (authenticated /dashboard)', () => {
  test('shows a standings row when the user has a leaderboard entry', async ({ page }) => {
    await mockAuth(page, {
      authUser: TEST_USER,
      navigateTo: '/dashboard',
      leaderboard: [{ userId: TEST_USER.userId, userName: TEST_USER.name, rank: '1', total: 150, weekResults: [] }],
    });
    await waitForSpinner(page);

    const standings = page.getByTestId('dashboard-standings');
    await expect(standings).toBeVisible({ timeout: 10_000 });
    await expect(standings.getByText('Test League')).toBeVisible();
    await expect(standings.getByText('+150')).toBeVisible();
  });

  test('shows an explicit empty state, not a blank panel, when the user has no leaderboard entry yet', async ({ page }) => {
    await mockAuth(page, { authUser: TEST_USER, navigateTo: '/dashboard', leaderboard: [] });
    await waitForSpinner(page);

    await expect(page.getByRole('heading', { name: /welcome back/i })).toBeVisible({ timeout: 10_000 });
    const standings = page.getByTestId('dashboard-standings');
    await expect(standings).toBeVisible();
    await expect(standings.getByText(/no nfl results yet this season/i)).toBeVisible();
  });
});
