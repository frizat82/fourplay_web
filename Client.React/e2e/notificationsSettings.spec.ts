import { test, expect } from '@playwright/test';
import { mockAuth } from './helpers/auth';

// Real service-worker registration/push subscription isn't meaningfully testable in this
// mocked Playwright harness (no real push service, no real Notification permission prompt) — this
// spec covers the preferences form (toggle interactions, simple/advanced expand, save-success
// toast), not the Enable push notifications flow itself.
test.describe('Notifications settings page (authenticated)', () => {
  test('reachable from Manage Account and renders the preferences form', async ({ page }) => {
    await mockAuth(page, { navigateTo: '/account/manage' });

    await page.getByRole('button', { name: /^notifications$/i }).click();
    await expect(page).toHaveURL(/\/account\/manage\/notifications/);
    await expect(page.getByLabel(/notify me about my games/i)).toBeVisible();
    await expect(page.getByLabel(/notify me about league activity/i)).toBeVisible();
    await expect(page.getByRole('button', { name: /save preferences/i })).toBeVisible();
  });

  test('simple toggle and advanced section expand, then save shows a success toast', async ({ page }) => {
    await mockAuth(page, { navigateTo: '/account/manage/notifications' });

    await expect(page.getByLabel(/notify me about my games/i)).not.toBeChecked();
    await page.getByLabel(/notify me about my games/i).click();
    await expect(page.getByLabel(/notify me about my games/i)).toBeChecked();

    await page.getByText(/advanced/i).click();
    await expect(page.getByRole('heading', { name: /my picks/i })).toBeVisible();
    await expect(page.getByRole('heading', { name: /other league members/i })).toBeVisible();

    await page.getByRole('button', { name: /save preferences/i }).click();
    await expect(page.getByText(/notification preferences saved/i)).toBeVisible({ timeout: 5000 });
  });
});
