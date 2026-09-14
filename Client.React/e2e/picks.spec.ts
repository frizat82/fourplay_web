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
    await expect(page.getByRole('button', { name: /^Pick \w/i }).first()).toBeVisible({ timeout: 5000 });
  });

  test('shows pick buttons for upcoming games', async ({ page }) => {
    await mockAuth(page, { navigateTo: '/picks' });

    await expect(page.getByRole('progressbar')).not.toBeVisible({ timeout: 10000 });

    // Games are in the future (gameStarted: false), so Pick buttons are not locked
    // Match "Pick BUF", "Pick MIA" etc. — not "Submit Pick(s)"
    const pickButtons = page.getByRole('button', { name: /^Pick \w/i });
    await expect(pickButtons.first()).toBeVisible({ timeout: 5000 });
    await expect(pickButtons.first()).toBeEnabled();

    // Should have 4 Pick buttons (2 games × 2 teams each)
    await expect(pickButtons).toHaveCount(4, { timeout: 5000 });
  });

  test('user can click a pick button and it toggles to Picked', async ({ page }) => {
    await mockAuth(page, { navigateTo: '/picks' });

    await expect(page.getByRole('progressbar')).not.toBeVisible({ timeout: 10000 });

    const firstPickButton = page.getByRole('button', { name: /^Pick \w/i }).first();
    await expect(firstPickButton).toBeVisible({ timeout: 5000 });
    await firstPickButton.click();

    // After clicking, the button should change to "{team} picked" (pending state)
    await expect(page.getByRole('button', { name: /\bpicked\b/i })).toBeVisible({ timeout: 3000 });
  });

  // frizat-immediate-pick-toggle: no more Submit/Clear step — a click writes to the backend
  // right away, and a picked-but-not-yet-kicked-off team stays clickable to unselect it.
  test('clicking a Pick button immediately calls POST /api/league/picks', async ({ page }) => {
    await mockAuth(page, { navigateTo: '/picks' });

    await expect(page.getByRole('progressbar')).not.toBeVisible({ timeout: 10000 });

    const picksPostRequest = page.waitForRequest(
      (req) => req.url().includes('/api/league/picks') && req.method() === 'POST'
    );

    await page.getByRole('button', { name: /^Pick \w/i }).first().click();

    const request = await picksPostRequest;
    expect(request.method()).toBe('POST');
    expect(request.url()).toContain('/api/league/picks');

    const body = JSON.parse(request.postData() ?? '[]') as unknown[];
    expect(body.length).toBeGreaterThan(0);
  });

  test('clicking a picked (unlocked) team immediately calls DELETE /api/league/picks/mine', async ({ page }) => {
    await mockAuth(page, { navigateTo: '/picks' });

    await expect(page.getByRole('progressbar')).not.toBeVisible({ timeout: 10000 });

    await page.getByRole('button', { name: /^Pick \w/i }).first().click();
    const pickedButton = page.getByRole('button', { name: /\bpicked\b/i }).first();
    await expect(pickedButton).toBeVisible({ timeout: 3000 });

    const removeRequest = page.waitForRequest(
      (req) => req.url().includes('/api/league/picks/mine') && req.method() === 'DELETE'
    );

    await pickedButton.click();

    const request = await removeRequest;
    expect(request.method()).toBe('DELETE');
  });

  test('never shows a Submit or Clear button', async ({ page }) => {
    await mockAuth(page, { navigateTo: '/picks' });

    await expect(page.getByRole('progressbar')).not.toBeVisible({ timeout: 10000 });
    await page.getByRole('button', { name: /^Pick \w/i }).first().click();

    await expect(page.getByRole('button', { name: /submit pick/i })).not.toBeVisible();
    await expect(page.getByRole('button', { name: /clear selected/i })).not.toBeVisible();
  });
});
