import { test, expect, type Page } from '@playwright/test';

const INVITE_CODE = 'test-invite-abc';

/**
 * Sets up mocks for the unauthenticated registration flow.
 * We only need to intercept /api/auth/create-user — no auth cookie required.
 */
async function setupRegistrationRoutes(page: Page, succeed = true) {
  await page.route('**/*', (route) => {
    const url = route.request().url();
    const method = route.request().method();

    if (!url.includes('/api/')) {
      void route.continue();
      return;
    }

    if (url.includes('/api/auth/create-user') && method === 'POST') {
      if (succeed) {
        void route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ isSuccess: true, errors: [] }),
        });
      } else {
        void route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ isSuccess: false, errors: ['Invalid invitation code'] }),
        });
      }
      return;
    }

    void route.continue();
  });
}

test.describe('Registration flow', () => {
  test('renders registration form with invite code pre-filled from URL', async ({ page }) => {
    await setupRegistrationRoutes(page);
    await page.goto(`/account/register?inviteCode=${INVITE_CODE}`);

    await expect(page.getByRole('heading', { name: /register/i })).toBeVisible({ timeout: 5000 });
    // Invitation code should be pre-populated from query param
    await expect(page.getByLabel(/invitation code/i)).toHaveValue(INVITE_CODE);
  });

  test('happy path: valid invite + credentials → register confirmation page', async ({ page }) => {
    await setupRegistrationRoutes(page, true);
    await page.goto(`/account/register?inviteCode=${INVITE_CODE}`);

    await page.getByLabel(/username/i).fill('newuser');
    await page.getByLabel(/^email/i).fill('newuser@example.com');
    await page.getByLabel(/^password$/i).fill('Test@1234');
    await page.getByLabel(/confirm password/i).fill('Test@1234');
    await page.getByRole('button', { name: /^register$/i }).click();

    await page.waitForURL('**/account/registerconfirmation**', { timeout: 10000 });
    await expect(page.getByRole('heading', { name: /register confirmation/i })).toBeVisible({ timeout: 5000 });
    await expect(page.getByText('newuser@example.com')).toBeVisible({ timeout: 5000 });
  });

  test('error path: invalid invite code shows error toast', async ({ page }) => {
    await setupRegistrationRoutes(page, false);
    await page.goto('/account/register?inviteCode=bad-code');

    await page.getByLabel(/username/i).fill('newuser');
    await page.getByLabel(/^email/i).fill('newuser@example.com');
    await page.getByLabel(/^password$/i).fill('Test@1234');
    await page.getByLabel(/confirm password/i).fill('Test@1234');
    await page.getByRole('button', { name: /^register$/i }).click();

    await expect(page.getByText(/invalid invitation code/i)).toBeVisible({ timeout: 5000 });
  });

  test('client validation: mismatched passwords show inline error', async ({ page }) => {
    await setupRegistrationRoutes(page);
    await page.goto(`/account/register?inviteCode=${INVITE_CODE}`);

    await page.getByLabel(/username/i).fill('newuser');
    await page.getByLabel(/^email/i).fill('newuser@example.com');
    await page.getByLabel(/^password$/i).fill('Test@1234');
    await page.getByLabel(/confirm password/i).fill('Different@5678');
    await page.getByRole('button', { name: /^register$/i }).click();

    await expect(page.getByText(/passwords do not match/i)).toBeVisible({ timeout: 5000 });
  });
});
