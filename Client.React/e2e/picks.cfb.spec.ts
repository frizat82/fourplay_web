import { test, expect } from '@playwright/test';
import { mockCfbAuth } from './helpers/cfbRoutes';

// CFB counterpart to picks.spec.ts. Runs under a cfb.localhost baseURL so
// useSportContext resolves the CFB adapter (see cfbRoutes.ts's mockCfbAuth).
test.use({ baseURL: 'http://cfb.localhost:5173' });

test.describe('CFB Picks page (authenticated)', () => {

  // OSU is seeded ranked #3 (setupCfbRoutes's cfbSpread fixture); the other three teams are unranked.
  test('shows AP rank next to a ranked team', async ({ page }) => {
    await mockCfbAuth(page, { navigateTo: '/picks' });

    await expect(page.getByRole('progressbar')).not.toBeVisible({ timeout: 10000 });
    await expect(page.getByText('#3')).toBeVisible({ timeout: 5000 });
  });

  test('shows pick buttons for upcoming games', async ({ page }) => {
    await mockCfbAuth(page, { navigateTo: '/picks' });

    await expect(page.getByRole('progressbar')).not.toBeVisible({ timeout: 10000 });
    await expect(page.getByRole('heading', { name: 'Picks', exact: true })).toBeVisible({ timeout: 5000 });

    const pickButtons = page.getByRole('button', { name: /^Pick \w/i });
    await expect(pickButtons.first()).toBeVisible({ timeout: 5000 });
    await expect(pickButtons.first()).toBeEnabled();
    await expect(pickButtons).toHaveCount(4, { timeout: 5000 });
  });

  // frizat-immediate-pick-toggle: no more Submit/Clear step — a click writes to the backend
  // right away, and a picked-but-not-yet-kicked-off team stays clickable to unselect it.
  test('clicking a Pick button immediately calls POST /api/cfb/picks', async ({ page }) => {
    await mockCfbAuth(page, { navigateTo: '/picks' });

    await expect(page.getByRole('progressbar')).not.toBeVisible({ timeout: 10000 });

    const picksPostRequest = page.waitForRequest(
      (req) => req.url().includes('/api/cfb/picks') && req.method() === 'POST'
    );

    await page.getByRole('button', { name: /^Pick \w/i }).first().click();

    const request = await picksPostRequest;
    expect(request.method()).toBe('POST');

    const body = JSON.parse(request.postData() ?? '{}') as { leagueId: number; cfbSlateId: number; picks: unknown[] };
    expect(body.picks.length).toBeGreaterThan(0);
  });

  test('clicking a picked (unlocked) team immediately calls DELETE /api/cfb/picks/mine', async ({ page }) => {
    await mockCfbAuth(page, { navigateTo: '/picks' });

    await expect(page.getByRole('progressbar')).not.toBeVisible({ timeout: 10000 });

    await page.getByRole('button', { name: /^Pick \w/i }).first().click();
    const pickedButton = page.getByRole('button', { name: /\bpicked\b/i }).first();
    await expect(pickedButton).toBeVisible({ timeout: 3000 });

    const removeRequest = page.waitForRequest(
      (req) => req.url().includes('/api/cfb/picks/mine') && req.method() === 'DELETE'
    );

    await pickedButton.click();

    const request = await removeRequest;
    expect(request.method()).toBe('DELETE');
  });

  test('never shows a Submit or Clear button', async ({ page }) => {
    await mockCfbAuth(page, { navigateTo: '/picks' });

    await expect(page.getByRole('progressbar')).not.toBeVisible({ timeout: 10000 });
    await page.getByRole('button', { name: /^Pick \w/i }).first().click();

    await expect(page.getByRole('button', { name: /submit pick/i })).not.toBeVisible();
    await expect(page.getByRole('button', { name: /clear selected/i })).not.toBeVisible();
  });
});
