import { test, expect, type Page } from '@playwright/test';
import { mockAuth, waitForSpinner, TEST_USER } from './helpers/auth';
import { setupRoutes } from './helpers/routes';

const MOCK_TOKEN = 'mocktokenabcdef1234567890abcdef12';
const MOCK_LEAGUE_NAME = 'Test League';

/**
 * Minimal route setup for unauthenticated pages.
 * Intercepts /api/auth/me (returns 401 so useAuth sees no user) and
 * /api/league/join/{token} (returns a valid invite link payload).
 */
async function setupUnauthRoutes(page: Page): Promise<void> {
  await page.route('**/*', (route) => {
    const url = route.request().url();
    const method = route.request().method();

    if (!url.includes('/api/')) {
      void route.continue();
      return;
    }

    if (url.includes('/api/auth/me') && method === 'GET') {
      void route.fulfill({ status: 401, contentType: 'application/json', body: 'null' });
      return;
    }

    if (url.includes('/api/auth/create-user') && method === 'POST') {
      void route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ isSuccess: true, errors: [] }),
      });
      return;
    }

    if (url.match(/\/api\/league\/join\/[^/]+$/) && method === 'GET') {
      void route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          token: MOCK_TOKEN,
          leagueId: 1,
          leagueName: MOCK_LEAGUE_NAME,
          expiresAt: new Date(Date.now() + 86400000).toISOString(),
        }),
      });
      return;
    }

    void route.continue();
  });
}

// ── Group 1: League portal — invite link generation ──────────────────────────

test.describe('League portal: Generate Invite Link', () => {
  test('shows Generate Invite Link button on Members tab when no link exists', async ({ page }) => {
    // routes.ts mocks GET /api/league/{id}/invite-link → 404 (no existing link)
    await mockAuth(page, { authUser: TEST_USER, navigateTo: '/league/manage' });
    await waitForSpinner(page);

    await expect(page.getByRole('button', { name: /generate invite link/i })).toBeVisible({ timeout: 5000 });
  });

  test('clicking Generate Invite Link shows the join URL and Copy/Share buttons', async ({ page }) => {
    // routes.ts mocks POST /api/league/{id}/invite-link → mockInviteLink() with MOCK_TOKEN
    await mockAuth(page, { authUser: TEST_USER, navigateTo: '/league/manage' });
    await waitForSpinner(page);

    await page.getByRole('button', { name: /generate invite link/i }).click();

    // The invite URL box should appear with /join/{token}
    await expect(page.getByText(new RegExp(`/join/${MOCK_TOKEN}`))).toBeVisible({ timeout: 5000 });
    await expect(page.getByRole('button', { name: /^copy$/i })).toBeVisible({ timeout: 3000 });
    await expect(page.getByRole('button', { name: /^share$/i })).toBeVisible({ timeout: 3000 });
  });

  test('button label changes to Regenerate Link after a link is generated', async ({ page }) => {
    await mockAuth(page, { authUser: TEST_USER, navigateTo: '/league/manage' });
    await waitForSpinner(page);

    await page.getByRole('button', { name: /generate invite link/i }).click();

    await expect(page.getByRole('button', { name: /regenerate link/i })).toBeVisible({ timeout: 5000 });
  });
});

// ── Group 2: Join page — unauthenticated user ─────────────────────────────────

test.describe('Join page: unauthenticated user', () => {
  test('shows league name from token validation', async ({ page }) => {
    await setupUnauthRoutes(page);
    await page.goto(`/join/${MOCK_TOKEN}`);

    await expect(page.getByText(MOCK_LEAGUE_NAME, { exact: false })).toBeVisible({ timeout: 5000 });
  });

  test('shows Create an account to join button when not logged in', async ({ page }) => {
    await setupUnauthRoutes(page);
    await page.goto(`/join/${MOCK_TOKEN}`);

    await expect(page.getByRole('button', { name: /create an account to join/i })).toBeVisible({ timeout: 5000 });
  });

  test('Create an account to join navigates to register page with inviteLinkToken param', async ({ page }) => {
    await setupUnauthRoutes(page);
    await page.goto(`/join/${MOCK_TOKEN}`);

    await page.getByRole('button', { name: /create an account to join/i }).click();

    await expect(page).toHaveURL(new RegExp(`inviteLinkToken=${MOCK_TOKEN}`), { timeout: 5000 });
    await expect(page).toHaveURL(/\/account\/register/, { timeout: 5000 });
  });

  test('shows error when token is invalid or expired', async ({ page }) => {
    await page.route('**/*', (route) => {
      const url = route.request().url();
      if (!url.includes('/api/')) { void route.continue(); return; }
      if (url.includes('/api/auth/me')) { void route.fulfill({ status: 401, body: 'null' }); return; }
      if (url.match(/\/api\/league\/join\/[^/]+$/) && route.request().method() === 'GET') {
        void route.fulfill({ status: 404, body: 'null' });
        return;
      }
      void route.continue();
    });
    await page.goto(`/join/bad-token`);

    await expect(page.getByText(/expired or invalid/i)).toBeVisible({ timeout: 5000 });
  });
});

// ── Group 3: Join page — authenticated user ───────────────────────────────────

test.describe('Join page: authenticated user', () => {
  test('shows Join League button when logged in', async ({ page }) => {
    // Use setupRoutes (no navigation) so routes are mocked, then set cookie and navigate
    await setupRoutes(page, { authUser: TEST_USER });
    await page.goto('/');
    await page.context().addCookies([
      { name: 'AuthToken', value: 'fake-jwt', domain: 'localhost', path: '/', httpOnly: false, secure: false, sameSite: 'Lax' },
    ]);
    await page.goto(`/join/${MOCK_TOKEN}`);

    // Authenticated user sees Join League, not Create an account
    await expect(page.getByRole('button', { name: /join league/i })).toBeVisible({ timeout: 5000 });
    await expect(page.getByRole('button', { name: /create an account/i })).not.toBeVisible();
  });

  test('clicking Join League redirects authenticated user to /dashboard', async ({ page }) => {
    await setupRoutes(page, { authUser: TEST_USER });
    await page.goto('/');
    await page.context().addCookies([
      { name: 'AuthToken', value: 'fake-jwt', domain: 'localhost', path: '/', httpOnly: false, secure: false, sameSite: 'Lax' },
    ]);
    await page.goto(`/join/${MOCK_TOKEN}`);

    await page.getByRole('button', { name: /join league/i }).click();

    await expect(page).toHaveURL(/\/dashboard/, { timeout: 8000 });
  });
});

// ── Group 4: Register page — inviteLinkToken flow ────────────────────────────

test.describe('Register page: invite link token flow', () => {
  test('hides Invitation Code field when inviteLinkToken is in the URL', async ({ page }) => {
    await setupUnauthRoutes(page);
    await page.goto(`/account/register?inviteLinkToken=${MOCK_TOKEN}&returnUrl=/join/${MOCK_TOKEN}`);

    // In the inviteLinkToken flow the code field is hidden (isLinkFlow = true)
    await expect(page.getByLabel(/invitation code/i)).not.toBeVisible({ timeout: 3000 });
    // Core fields are still shown
    await expect(page.getByLabel(/username/i)).toBeVisible();
    await expect(page.getByLabel(/^email/i)).toBeVisible();
  });

  test('happy path with inviteLinkToken: submit → confirmation page', async ({ page }) => {
    await setupUnauthRoutes(page);
    await page.goto(`/account/register?inviteLinkToken=${MOCK_TOKEN}&returnUrl=/join/${MOCK_TOKEN}`);

    await page.getByLabel(/username/i).fill('newplayer');
    await page.getByLabel(/^email/i).fill('newplayer@example.com');
    await page.getByLabel(/^password$/i).fill('Test@1234');
    await page.getByLabel(/confirm password/i).fill('Test@1234');
    await page.getByRole('button', { name: /^register$/i }).click();

    await page.waitForURL('**/account/registerconfirmation**', { timeout: 10000 });
    await expect(page.getByRole('heading', { name: /register confirmation/i })).toBeVisible({ timeout: 5000 });
    await expect(page.getByText('newplayer@example.com')).toBeVisible();
  });
});

// ── Group 5: Full journey ─────────────────────────────────────────────────────

test.describe('Full invite link journey', () => {
  test('owner generates link → new user navigates to join page → sees league name and sign-up CTA', async ({ page }) => {
    // Step 1: league owner generates the link
    await mockAuth(page, { authUser: TEST_USER, navigateTo: '/league/manage' });
    await waitForSpinner(page);

    await page.getByRole('button', { name: /generate invite link/i }).click();

    // Step 2: confirm the invite URL is shown in the portal
    const linkLocator = page.getByText(new RegExp(`/join/${MOCK_TOKEN}`));
    await expect(linkLocator).toBeVisible({ timeout: 5000 });

    // Step 3: simulate an unauthenticated user navigating to that join URL.
    // We need to clear auth state and set up unauth routes, then re-navigate.
    await page.context().clearCookies();
    // Add unauthenticated route overlay (new intercept wins because Playwright
    // calls handlers in registration order; existing handler already handles non-/api/ paths)
    await page.route('**/api/auth/me**', (route) => {
      void route.fulfill({ status: 401, body: 'null' });
    });

    await page.goto(`/join/${MOCK_TOKEN}`);

    await expect(page.getByText(MOCK_LEAGUE_NAME, { exact: false })).toBeVisible({ timeout: 5000 });
    await expect(page.getByRole('button', { name: /create an account to join/i })).toBeVisible({ timeout: 5000 });
  });
});
