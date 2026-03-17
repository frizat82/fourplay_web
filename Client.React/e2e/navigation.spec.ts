import { test, expect } from '@playwright/test';

test.describe('Unauthenticated navigation', () => {
  test('redirects /picks to login when not authenticated', async ({ page }) => {
    await page.goto('/picks');
    await expect(page).toHaveURL(/login/);
  });

  test('redirects /scores to login when not authenticated', async ({ page }) => {
    await page.goto('/scores');
    await expect(page).toHaveURL(/login/);
  });

  test('redirects /leaderboard to login when not authenticated', async ({ page }) => {
    await page.goto('/leaderboard');
    await expect(page).toHaveURL(/login/);
  });

  test('forgot password page renders without auth', async ({ page }) => {
    await page.goto('/account/forgotpassword');
    await expect(page.getByLabel(/email/i)).toBeVisible();
  });
});
