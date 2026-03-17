import { test, expect } from '@playwright/test';
import { mockAuth } from './helpers/auth';

test.describe('Picks page (authenticated)', () => {
  test('renders picks page for authenticated user', async ({ page }) => {
    await mockAuth(page, { navigateTo: '/picks' });

    // Wait for loading spinner to disappear
    await expect(page.getByRole('progressbar')).not.toBeVisible({ timeout: 10000 });

    // Page header should show "Picks" — use exact match to avoid matching "Picks Remaining"
    await expect(page.getByRole('heading', { name: 'Picks', exact: true })).toBeVisible({ timeout: 5000 });

    // Game rows should be rendered (BUF/MIA and DAL/NYG)
    await expect(page.getByRole('button', { name: 'Pick', exact: true }).first()).toBeVisible({ timeout: 5000 });
  });

  test('shows pick buttons for upcoming games', async ({ page }) => {
    await mockAuth(page, { navigateTo: '/picks' });

    await expect(page.getByRole('progressbar')).not.toBeVisible({ timeout: 10000 });

    // Games are in the future (gameStarted: false), so Pick buttons are not locked
    // Use exact: true to match "Pick" not "Submit Pick(s)"
    const pickButtons = page.getByRole('button', { name: 'Pick', exact: true });
    await expect(pickButtons.first()).toBeVisible({ timeout: 5000 });
    await expect(pickButtons.first()).toBeEnabled();

    // Should have 4 Pick buttons (2 games × 2 teams each)
    await expect(pickButtons).toHaveCount(4, { timeout: 5000 });
  });

  test('user can click a pick button and it toggles to Picked', async ({ page }) => {
    await mockAuth(page, { navigateTo: '/picks' });

    await expect(page.getByRole('progressbar')).not.toBeVisible({ timeout: 10000 });

    const firstPickButton = page.getByRole('button', { name: 'Pick', exact: true }).first();
    await expect(firstPickButton).toBeVisible({ timeout: 5000 });
    await firstPickButton.click();

    // After clicking, the button should change to "Picked"
    await expect(page.getByRole('button', { name: 'Picked', exact: true })).toBeVisible({ timeout: 3000 });
  });

  test('submit button disabled with no picks selected', async ({ page }) => {
    await mockAuth(page, { navigateTo: '/picks' });

    await expect(page.getByRole('progressbar')).not.toBeVisible({ timeout: 10000 });

    // The submit button text is "Submit Pick(s)"
    const submitButton = page.getByRole('button', { name: /submit pick\(s\)/i });
    await expect(submitButton).toBeVisible({ timeout: 5000 });
    await expect(submitButton).toBeDisabled();
  });

  test('submit button enabled after picking a team', async ({ page }) => {
    await mockAuth(page, { navigateTo: '/picks' });

    await expect(page.getByRole('progressbar')).not.toBeVisible({ timeout: 10000 });

    // Submit should start disabled
    const submitButton = page.getByRole('button', { name: /submit pick\(s\)/i });
    await expect(submitButton).toBeDisabled({ timeout: 5000 });

    // Click the first Pick button (exact match to avoid hitting "Submit Pick(s)")
    await page.getByRole('button', { name: 'Pick', exact: true }).first().click();

    // Submit should now be enabled
    await expect(submitButton).toBeEnabled({ timeout: 3000 });
  });

  test('submitting picks calls POST /api/league/picks', async ({ page }) => {
    await mockAuth(page, { navigateTo: '/picks' });

    await expect(page.getByRole('progressbar')).not.toBeVisible({ timeout: 10000 });

    // Click the first Pick button to select a team
    await page.getByRole('button', { name: 'Pick', exact: true }).first().click();

    // Wait for the submit button to become enabled
    const submitButton = page.getByRole('button', { name: /submit pick\(s\)/i });
    await expect(submitButton).toBeEnabled({ timeout: 3000 });

    // Set up a promise to capture the POST request before clicking submit
    const picksPostRequest = page.waitForRequest(
      (req) => req.url().includes('/api/league/picks') && req.method() === 'POST'
    );

    await submitButton.click();

    // Verify the POST request was made
    const request = await picksPostRequest;
    expect(request.method()).toBe('POST');
    expect(request.url()).toContain('/api/league/picks');

    // Verify the request body contains picks
    const body = JSON.parse(request.postData() ?? '[]') as unknown[];
    expect(body.length).toBeGreaterThan(0);
  });
});
