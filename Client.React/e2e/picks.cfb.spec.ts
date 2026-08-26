import { test, expect } from '@playwright/test';
import { mockCfbAuth } from './helpers/cfbRoutes';

// CFB counterpart to picks.spec.ts. Runs under a cfb.localhost baseURL so
// useSportContext resolves the CFB adapter (see cfbRoutes.ts's mockCfbAuth).
test.use({ baseURL: 'http://cfb.localhost:5173' });

test.describe('CFB Picks page (authenticated)', () => {
  test('renders picks page for authenticated user', async ({ page }) => {
    await mockCfbAuth(page, { navigateTo: '/picks' });

    await expect(page.getByRole('progressbar')).not.toBeVisible({ timeout: 10000 });
    await expect(page.getByRole('heading', { name: 'Picks', exact: true })).toBeVisible({ timeout: 5000 });

    // 2 games x 2 teams = 4 Pick buttons (OSU/MICH, ALA/UGA)
    await expect(page.getByRole('button', { name: /^Pick \w/i }).first()).toBeVisible({ timeout: 5000 });
  });

  test('shows pick buttons for upcoming games', async ({ page }) => {
    await mockCfbAuth(page, { navigateTo: '/picks' });

    await expect(page.getByRole('progressbar')).not.toBeVisible({ timeout: 10000 });

    const pickButtons = page.getByRole('button', { name: /^Pick \w/i });
    await expect(pickButtons.first()).toBeVisible({ timeout: 5000 });
    await expect(pickButtons.first()).toBeEnabled();
    await expect(pickButtons).toHaveCount(4, { timeout: 5000 });
  });

  test('user can click a pick button and it toggles to Picked', async ({ page }) => {
    await mockCfbAuth(page, { navigateTo: '/picks' });

    await expect(page.getByRole('progressbar')).not.toBeVisible({ timeout: 10000 });

    const firstPickButton = page.getByRole('button', { name: /^Pick \w/i }).first();
    await expect(firstPickButton).toBeVisible({ timeout: 5000 });
    await firstPickButton.click();

    await expect(page.getByRole('button', { name: /\bpicked\b/i })).toBeVisible({ timeout: 3000 });
  });

  test('submit button disabled with no picks selected', async ({ page }) => {
    await mockCfbAuth(page, { navigateTo: '/picks' });

    await expect(page.getByRole('progressbar')).not.toBeVisible({ timeout: 10000 });

    const submitButton = page.getByRole('button', { name: /submit pick\(s\)/i });
    await expect(submitButton).toBeVisible({ timeout: 5000 });
    await expect(submitButton).toBeDisabled();
  });

  test('submitting picks calls POST /api/cfb/picks', async ({ page }) => {
    await mockCfbAuth(page, { navigateTo: '/picks' });

    await expect(page.getByRole('progressbar')).not.toBeVisible({ timeout: 10000 });

    await page.getByRole('button', { name: /^Pick \w/i }).first().click();

    const submitButton = page.getByRole('button', { name: /submit pick\(s\)/i });
    await expect(submitButton).toBeEnabled({ timeout: 3000 });

    const picksPostRequest = page.waitForRequest(
      (req) => req.url().includes('/api/cfb/picks') && req.method() === 'POST'
    );

    await submitButton.click();

    const request = await picksPostRequest;
    expect(request.method()).toBe('POST');

    const body = JSON.parse(request.postData() ?? '{}') as { leagueId: number; cfbSlateId: number; picks: unknown[] };
    expect(body.picks.length).toBeGreaterThan(0);
  });
});
