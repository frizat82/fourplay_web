import { test, expect } from '@playwright/test';
import { mockAuth, ADMIN_USER, waitForSpinner } from './helpers/auth';

const adminAuth = (page: Parameters<typeof mockAuth>[0], navigateTo: string) =>
  mockAuth(page, { authUser: ADMIN_USER, navigateTo });

test.describe('Admin pages (administrator role)', () => {
  // -----------------------------------------------------------------------
  // Job Manager
  // -----------------------------------------------------------------------
  test('Job Manager renders heading and job table', async ({ page }) => {
    await adminAuth(page, '/admin/jobManager');
    await waitForSpinner(page);

    await expect(page.getByRole('heading', { name: /^job manager$/i })).toBeVisible({ timeout: 5000 });
    await expect(page.getByText('NflScoresJob')).toBeVisible({ timeout: 5000 });
  });

  test('Job Manager groups jobs by category and hides background jobs behind a toggle', async ({ page }) => {
    await adminAuth(page, '/admin/jobManager');
    await waitForSpinner(page);

    await expect(page.getByText('NFL Scores', { exact: true })).toBeVisible({ timeout: 5000 });
    await expect(page.getByText('NflScoresJob')).toBeVisible();
    await expect(page.getByText('Juice Reminder 6-2026')).not.toBeVisible();

    await page.getByLabel(/show background jobs/i).click();

    await expect(page.getByText('Juice Reminder 6-2026')).toBeVisible();
    await expect(page.getByText('Remind "Sunday Funday" owner to configure Juice for season 2026')).toBeVisible();
  });

  // -----------------------------------------------------------------------
  // User Management
  // -----------------------------------------------------------------------
  test('User Management renders heading and user rows', async ({ page }) => {
    await adminAuth(page, '/admin/users');
    await waitForSpinner(page);

    await expect(page.getByRole('heading', { name: /user management/i })).toBeVisible({ timeout: 5000 });
    await expect(page.getByText('alice@example.com')).toBeVisible({ timeout: 5000 });
    await expect(page.getByText('bob@example.com')).toBeVisible({ timeout: 5000 });
  });

  // -----------------------------------------------------------------------
  // Invitations
  // -----------------------------------------------------------------------
  test('Invitations page renders heading and invitation row', async ({ page }) => {
    await adminAuth(page, '/admin/invitations');
    await waitForSpinner(page);

    await expect(page.getByRole('heading', { name: /manage invitations/i })).toBeVisible({ timeout: 5000 });
    await expect(page.getByText('invite@example.com')).toBeVisible({ timeout: 5000 });
  });

  // -----------------------------------------------------------------------
  // League Costs
  // -----------------------------------------------------------------------
  test('League Costs page renders heading, league row, and total', async ({ page }) => {
    await adminAuth(page, '/admin/leagueCosts');
    await waitForSpinner(page);

    await expect(page.getByRole('heading', { name: /league costs/i })).toBeVisible({ timeout: 5000 });
    await expect(page.getByRole('cell', { name: 'Test League' })).toBeVisible({ timeout: 5000 });
    await expect(page.getByText('commish')).toBeVisible({ timeout: 5000 });
    await expect(page.getByText('$120').first()).toBeVisible({ timeout: 5000 });
  });

  // -----------------------------------------------------------------------
  // My Leagues — admin-only actions (Create League / Add User / Change Owner)
  // -----------------------------------------------------------------------
  test('My Leagues — Create League button opens Create League dialog', async ({ page }) => {
    await adminAuth(page, '/league/manage');
    await waitForSpinner(page);

    await page.getByRole('button', { name: /create league/i }).click();
    await expect(page.getByRole('dialog')).toBeVisible({ timeout: 3000 });
    await expect(page.getByRole('dialog').getByRole('heading', { name: /create league/i })).toBeVisible({ timeout: 3000 });
  });

  test('My Leagues — Add User button opens Add User dialog', async ({ page }) => {
    await adminAuth(page, '/league/manage');
    await waitForSpinner(page);

    await page.getByRole('button', { name: /add user/i }).click();
    await expect(page.getByRole('dialog')).toBeVisible({ timeout: 3000 });
    await expect(page.getByRole('dialog').getByRole('heading', { name: /add user to/i })).toBeVisible({ timeout: 3000 });
  });

  test('My Leagues — Change Owner button opens Change Owner dialog', async ({ page }) => {
    await adminAuth(page, '/league/manage');
    await waitForSpinner(page);

    await page.getByRole('tab', { name: 'Info' }).click();
    await page.getByRole('button', { name: /change owner/i }).click();
    await expect(page.getByRole('dialog')).toBeVisible({ timeout: 3000 });
    await expect(page.getByRole('dialog').getByRole('heading', { name: /change owner/i })).toBeVisible({ timeout: 3000 });
  });

  // -----------------------------------------------------------------------
  // Non-admin is redirected away from admin pages
  // -----------------------------------------------------------------------
  test('non-admin user is redirected to dashboard', async ({ page }) => {
    // Use default TEST_USER (no admin role)
    await mockAuth(page, { navigateTo: '/admin/users' });
    // Should be redirected to /dashboard
    await page.waitForURL('**/dashboard', { timeout: 5000 });
  });
});
