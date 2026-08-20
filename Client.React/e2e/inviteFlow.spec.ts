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
          leagueId: 99,
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

  test('Revoke Link shuts off the current link without generating a replacement', async ({ page }) => {
    // Unlike Regenerate, a commissioner should be able to just stop accepting new signups
    // without immediately producing a new link they'd have to reshare.
    await mockAuth(page, { authUser: TEST_USER, navigateTo: '/league/manage' });
    await waitForSpinner(page);

    await page.getByRole('button', { name: /generate invite link/i }).click();
    await expect(page.getByText(new RegExp(`/join/${MOCK_TOKEN}`))).toBeVisible({ timeout: 5000 });

    await page.getByRole('button', { name: /revoke link/i }).click();

    await expect(page.getByText(new RegExp(`/join/${MOCK_TOKEN}`))).not.toBeVisible({ timeout: 5000 });
    await expect(page.getByRole('button', { name: /generate invite link/i })).toBeVisible({ timeout: 5000 });
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

// ── Group 4b: Join page — already-a-member (409) ─────────────────────────────

test.describe('Join page: already-a-member (409)', () => {
  test('409 from joinViaLink still navigates to /dashboard — already-member is a no-op', async ({ page }) => {
    // Override the join POST to return 409 (already a member) — the handler in
    // setupRoutes returns 204; registering this before setupRoutes wins because
    // Playwright calls route handlers in registration order (LIFO).
    await page.route(/\/api\/league\/join\/[^/]+$/, (route) => {
      if (route.request().method() === 'POST') {
        void route.fulfill({ status: 409 });
        return;
      }
      void route.continue();
    });

    await setupRoutes(page, { authUser: TEST_USER });
    await page.goto('/');
    await page.context().addCookies([
      { name: 'AuthToken', value: 'fake-jwt', domain: 'localhost', path: '/', httpOnly: false, secure: false, sameSite: 'Lax' },
    ]);
    await page.goto(`/join/${MOCK_TOKEN}`);

    await page.getByRole('button', { name: /join league/i }).click();

    // 409 should be treated as "already a member" — user lands on dashboard, not an error
    await expect(page).toHaveURL(/\/dashboard/, { timeout: 8000 });
    await expect(page.getByText(/expired|invalid|failed/i)).not.toBeVisible();
  });
});

// ── Group 4c: Invite Player (email invite) dialog ─────────────────────────────

test.describe('League portal: Invite Player (email) dialog', () => {
  async function setupWithInviteRoute(
    page: Page,
    outcome: 'invited' | 'existingUserPending' | 'conflict' | 'error' = 'invited',
  ): Promise<void> {
    await mockAuth(page, { authUser: TEST_USER, navigateTo: '/league/manage' });

    // Registered after mockAuth/setupRoutes so it wins — Playwright runs the most-recently
    // registered route handler first. setupRoutes' own POST /invite mock always fulfills (never
    // calls route.fallback()), so registering this before mockAuth was silently a no-op: every
    // outcome here was masked by that generic {} success body.
    await page.route(/\/api\/league\/\d+\/invite$/, (route) => {
      if (route.request().method() === 'POST') {
        if (outcome === 'error') {
          void route.fulfill({ status: 500 });
          return;
        }
        if (outcome === 'conflict') {
          void route.fulfill({
            status: 409,
            contentType: 'application/json',
            body: JSON.stringify('friend@example.com is already a member of this league.'),
          });
          return;
        }
        void route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            email: 'friend@example.com',
            outcome: outcome === 'existingUserPending' ? 'ExistingUserInvitePending' : 'NewUserInvitationSent',
          }),
        });
        return;
      }
      void route.continue();
    });
    await waitForSpinner(page);
  }

  test('clicking Invite Player opens a dialog with an email field', async ({ page }) => {
    await setupWithInviteRoute(page);

    await page.getByRole('button', { name: /invite player/i }).click();

    await expect(page.getByRole('dialog')).toBeVisible({ timeout: 5000 });
    await expect(page.getByLabel(/email/i)).toBeVisible({ timeout: 3000 });
  });

  test('Invite Player submit button is disabled when email is empty', async ({ page }) => {
    await setupWithInviteRoute(page);

    await page.getByRole('button', { name: /invite player/i }).click();
    await expect(page.getByRole('dialog')).toBeVisible({ timeout: 5000 });

    // Submit inside the dialog (not the outer form) — button is disabled with empty email
    const sendBtn = page.getByRole('dialog').getByRole('button', { name: /send invite|invite/i });
    await expect(sendBtn).toBeDisabled({ timeout: 3000 });
  });

  test('filling email and submitting sends invite and closes dialog', async ({ page }) => {
    await setupWithInviteRoute(page, 'invited');

    await page.getByRole('button', { name: /invite player/i }).click();
    await expect(page.getByRole('dialog')).toBeVisible({ timeout: 5000 });

    await page.getByLabel(/email/i).fill('friend@example.com');
    await page.getByRole('dialog').getByRole('button', { name: /send invite|invite/i }).click();

    // Dialog closes and a success toast appears
    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 5000 });
    await expect(page.getByRole('alert')).toContainText(/invitation sent/i, { timeout: 5000 });
  });

  test('inviting an already-registered email creates a pending invite, with a distinct message', async ({ page }) => {
    // The whole point of this feature: someone with an existing account (owns a league or
    // belongs to one) doesn't need to re-register — but unlike a brand-new user, they get a
    // pending invite they must explicitly accept, not instant membership with no consent.
    await setupWithInviteRoute(page, 'existingUserPending');

    await page.getByRole('button', { name: /invite player/i }).click();
    await page.getByLabel(/email/i).fill('friend@example.com');
    await page.getByRole('dialog').getByRole('button', { name: /send invite|invite/i }).click();

    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 5000 });
    await expect(page.getByRole('alert')).toContainText(/pending their acceptance/i, { timeout: 5000 });
    await expect(page.getByRole('alert')).not.toContainText(/^invitation sent/i);
  });

  test('inviting an email already on the league shows the server\'s specific conflict message', async ({ page }) => {
    await setupWithInviteRoute(page, 'conflict');

    await page.getByRole('button', { name: /invite player/i }).click();
    await page.getByLabel(/email/i).fill('friend@example.com');
    await page.getByRole('dialog').getByRole('button', { name: /send invite|invite/i }).click();

    // Not getByRole('alert') here: on this error path the Dialog stays open (only success
    // closes it), and MUI applies aria-hidden to background siblings — including the toast —
    // while a modal is open, so a role-based query finds nothing even though it's rendered.
    await expect(page.locator('.MuiAlert-root')).toContainText(/already a member of this league/i, { timeout: 5000 });
  });
});

// ── Group 4d: Pending membership invite banner (invitee-facing) ───────────────

test.describe('Pending membership invite banner', () => {
  test('shows on any authenticated page; Accept clears it', async ({ page }) => {
    await mockAuth(page, { authUser: TEST_USER, navigateTo: '/picks' });

    let resolved = false;
    // Registered after mockAuth so it wins over setupRoutes' default empty-array response.
    await page.route(/\/api\/league\/membership-invites\/mine$/, (route) => {
      if (route.request().method() !== 'GET') return void route.continue();
      void route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(resolved ? [] : [{
          id: 9, leagueId: 1, leagueName: 'Rival League', invitedByUserName: 'commish',
          createdAt: new Date().toISOString(),
        }]),
      });
    });
    await page.route(/\/api\/league\/membership-invites\/\d+\/accept$/, (route) => {
      if (route.request().method() !== 'POST') return void route.continue();
      resolved = true;
      void route.fulfill({ status: 204 });
    });

    await page.reload();
    await waitForSpinner(page);

    const banner = page.getByRole('alert').filter({ hasText: /rival league/i });
    await expect(banner).toBeVisible({ timeout: 5000 });
    await expect(banner).toContainText(/you're being asked to join/i);

    await page.getByRole('button', { name: /^accept$/i }).click();

    await expect(banner).not.toBeVisible({ timeout: 5000 });
  });

  test('Decline clears the banner without adding the league', async ({ page }) => {
    await mockAuth(page, { authUser: TEST_USER, navigateTo: '/picks' });

    let resolved = false;
    let declineCalled = false;
    await page.route(/\/api\/league\/membership-invites\/mine$/, (route) => {
      if (route.request().method() !== 'GET') return void route.continue();
      void route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(resolved ? [] : [{
          id: 9, leagueId: 1, leagueName: 'Rival League', invitedByUserName: 'commish',
          createdAt: new Date().toISOString(),
        }]),
      });
    });
    await page.route(/\/api\/league\/membership-invites\/\d+\/decline$/, (route) => {
      if (route.request().method() !== 'POST') return void route.continue();
      resolved = true;
      declineCalled = true;
      void route.fulfill({ status: 204 });
    });

    await page.reload();
    await waitForSpinner(page);

    const banner = page.getByRole('alert').filter({ hasText: /rival league/i });
    await expect(banner).toBeVisible({ timeout: 5000 });

    await page.getByRole('button', { name: /^decline$/i }).click();

    await expect(banner).not.toBeVisible({ timeout: 5000 });
    expect(declineCalled).toBe(true);
  });
});

// ── Group 4e: Registration confirmation — always shows Go to login ────────────

test.describe('Registration confirmation page', () => {
  test('shows Go to login button (not returnUrl redirect) — email confirmation step is required first', async ({ page }) => {
    await setupUnauthRoutes(page);

    // Navigate directly to the confirmation page with a returnUrl (as RegisterPage would)
    await page.goto(`/account/registerconfirmation?email=newplayer%40example.com&returnUrl=${encodeURIComponent('/join/' + MOCK_TOKEN)}`);

    await expect(page.getByRole('heading', { name: /register confirmation/i })).toBeVisible({ timeout: 5000 });
    // Confirmation page intentionally ignores returnUrl — user must confirm email first
    await expect(page.getByRole('button', { name: /go to login/i })).toBeVisible({ timeout: 3000 });
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
