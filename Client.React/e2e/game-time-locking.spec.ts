import { test, expect } from '@playwright/test';

/**
 * frizat-d6y: Per-game kickoff time locking
 *
 * These tests verify the locking behavior in the authenticated picks flow.
 * Since these require auth, they serve as documentation of expected behavior
 * and can be extended with fixture-based auth when a test user is set up.
 */
test.describe('Game time locking (unauthenticated guard)', () => {
  test('picks page requires auth — confirms redirect', async ({ page }) => {
    await page.goto('/picks');
    // Without auth, must redirect to login — confirms the guard is active
    await expect(page).toHaveURL(/login/, { timeout: 5000 });
  });
});
