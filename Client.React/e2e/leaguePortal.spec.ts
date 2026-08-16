import { test, expect } from '@playwright/test';
import { mockAuth, waitForSpinner, TEST_USER, ADMIN_USER, NON_OWNER_USER } from './helpers/auth';

// TEST_USER is wired in routes.ts to own "Test League" (NFL) via /api/league/my-leagues
const ownerAuth = (page: Parameters<typeof mockAuth>[0]) =>
  mockAuth(page, { authUser: TEST_USER, navigateTo: '/league/manage' });

// NON_OWNER_USER is a regular (non-admin) user who owns no leagues.
const nonOwnerAuth = (page: Parameters<typeof mockAuth>[0]) =>
  mockAuth(page, { authUser: NON_OWNER_USER, navigateTo: '/league/manage' });

// ADMIN_USER also owns no leagues via /api/league/my-leagues, but admins can
// manage every league in the system (see /api/league/all-leagues), so My
// Leagues stays visible/usable for them regardless of personal ownership.
const adminAuth = (page: Parameters<typeof mockAuth>[0]) =>
  mockAuth(page, { authUser: ADMIN_USER, navigateTo: '/league/manage' });

test.describe('Commissioner portal (/league/manage)', () => {
  test('league owner sees portal with My Leagues heading', async ({ page }) => {
    await ownerAuth(page);
    await waitForSpinner(page);

    await expect(page.getByRole('heading', { name: /my leagues/i })).toBeVisible({ timeout: 5000 });
  });

  // Self-serve league creation (frizat-d6l): a user with no leagues yet still reaches the
  // real page (not a dead end) and gets a Create League call to action.
  test('user with no leagues sees the portal with a Create League empty state', async ({ page }) => {
    await nonOwnerAuth(page);
    await waitForSpinner(page);

    await expect(page.getByRole('heading', { name: /my leagues/i })).toBeVisible({ timeout: 5000 });
    await expect(page.getByText(/you don.t have any leagues yet/i)).toBeVisible({ timeout: 5000 });
    await expect(page.getByRole('button', { name: /create league/i })).toBeVisible();
  });

  test('Members tab loads and shows cost chip', async ({ page }) => {
    await ownerAuth(page);
    await waitForSpinner(page);

    // Members tab is selected by default
    await expect(page.getByRole('tab', { name: /members/i })).toBeVisible();
    // Cost chip — 5 members · $100/season from mocked /cost endpoint. Scoped text — the header's
    // OwnerCostSummary chip also shows "$100" (single owned league), so a bare /\$100/ is ambiguous.
    await expect(page.getByText(/members.*\$100\/season/)).toBeVisible({ timeout: 5000 });
  });

  test('League Payouts tab loads existing juice values', async ({ page }) => {
    await ownerAuth(page);
    await waitForSpinner(page);

    await page.getByRole('tab', { name: /league payouts/i }).click();
    await waitForSpinner(page);

    // Mocked juice: 13 pts tease — "Tease Pts (Regular Season)" label should be visible
    await expect(page.getByLabel(/tease pts \(regular season\)/i)).toBeVisible({ timeout: 5000 });
  });

  test('My Leagues nav link is visible for league owner', async ({ page }) => {
    await ownerAuth(page);
    await waitForSpinner(page);

    await expect(page.getByRole('link', { name: /my leagues/i })).toBeVisible({ timeout: 5000 });
  });

  test('My Leagues nav link is visible for a user who owns no leagues (self-serve creation)', async ({ page }) => {
    await nonOwnerAuth(page);
    await waitForSpinner(page);

    await expect(page.getByRole('link', { name: /my leagues/i })).toBeVisible({ timeout: 5000 });
  });

  test('My Leagues nav link stays visible for an admin who owns no leagues', async ({ page }) => {
    await adminAuth(page);
    await waitForSpinner(page);

    await expect(page.getByRole('link', { name: /my leagues/i })).toBeVisible({ timeout: 5000 });
    await expect(page.getByRole('heading', { name: /my leagues/i })).toBeVisible({ timeout: 5000 });
  });

  test('Invite dialog opens and accepts email input', async ({ page }) => {
    await ownerAuth(page);
    await waitForSpinner(page);

    await page.getByRole('button', { name: /invite player/i }).click();
    await expect(page.getByRole('dialog')).toBeVisible({ timeout: 3000 });
    await page.getByRole('dialog').getByRole('textbox').fill('newplayer@test.com');
    await expect(page.getByRole('dialog').getByRole('textbox')).toHaveValue('newplayer@test.com');
  });
});
