import { expect, type Page } from '@playwright/test';
import { setupRoutes, TEST_USER, ADMIN_USER } from './routes';
import type { SetupRoutesOptions } from './routes';

/**
 * A fake JWT string. The server is mocked so no real JWT validation happens.
 * The app reads the AuthToken cookie to know a session may exist, then hits
 * /api/auth/me (which is mocked to return TEST_USER) to hydrate the auth
 * context. Presence of the cookie alone satisfies the client-side cookie check.
 */
const FAKE_JWT =
  'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.' +
  'eyJzdWIiOiJ0ZXN0LXVzZXItaWQtMDAxIiwibmFtZSI6IlRlc3RVc2VyIiwiaWF0IjoxNzAwMDAwMDAwfQ.' +
  'fake-signature-for-e2e-tests-only';

export interface MockAuthOptions extends SetupRoutesOptions {
  /** Path to navigate to after auth is set up. Defaults to '/picks'. */
  navigateTo?: string;
}

/**
 * Sets up all API route mocks and injects the AuthToken cookie so the app
 * believes a user is authenticated. After calling this, navigate to any
 * protected page — the mocked /api/auth/me will hydrate the auth context.
 */
export async function mockAuth(page: Page, options: MockAuthOptions = {}): Promise<void> {
  const { navigateTo, ...routeOptions } = options;

  // Set up all route intercepts first
  await setupRoutes(page, routeOptions);

  // Inject a fake AuthToken cookie. The app checks this cookie to decide
  // whether to call /api/auth/me at startup. The mocked route handles the rest.
  await page.goto('/');

  await page.context().addCookies([
    {
      name: 'AuthToken',
      value: FAKE_JWT,
      domain: 'localhost',
      path: '/',
      httpOnly: false, // Playwright can only set non-httpOnly cookies this way;
      // the mocked /api/auth/me is what actually establishes auth state
      secure: false,
      sameSite: 'Lax',
    },
  ]);

  if (navigateTo) {
    await page.goto(navigateTo);
  }
}

/** Wait for the MUI CircularProgress spinner to disappear. */
export async function waitForSpinner(page: Page): Promise<void> {
  await expect(page.getByRole('progressbar')).not.toBeVisible({ timeout: 10000 });
}

export { TEST_USER, ADMIN_USER };
