import { test, expect } from '@playwright/test';
import { mockAuth } from './helpers/auth';

test.describe('Change Username page (authenticated)', () => {
  test('reachable from Manage Account and renders the form', async ({ page }) => {
    await mockAuth(page, { navigateTo: '/account/manage' });

    await page.getByRole('button', { name: /change username/i }).click();
    await expect(page).toHaveURL(/\/account\/manage\/changeusername/);
    await expect(page.getByLabel(/current password/i)).toBeVisible();
    await expect(page.getByLabel(/new username/i)).toBeVisible();
    await expect(page.getByRole('button', { name: /update username/i })).toBeVisible();
  });

  test('shows validation errors on empty submit', async ({ page }) => {
    await mockAuth(page, { navigateTo: '/account/manage/changeusername' });

    await page.getByRole('button', { name: /update username/i }).click();
    await expect(page.getByText(/current password is required/i)).toBeVisible({ timeout: 3000 });
    await expect(page.getByText(/username is required/i)).toBeVisible({ timeout: 3000 });
  });

  test('happy path: submitting valid values shows a success toast', async ({ page }) => {
    await mockAuth(page, { navigateTo: '/account/manage/changeusername' });

    await page.getByLabel(/current password/i).fill('CorrectPass1!');
    await page.getByLabel(/new username/i).fill('newusername');
    await page.getByRole('button', { name: /update username/i }).click();

    await expect(page.getByText(/username updated/i)).toBeVisible({ timeout: 5000 });
  });
});
