import { test, expect } from '@playwright/test';

test.describe('Home page', () => {
  test('renders IV League hero section', async ({ page }) => {
    await page.goto('/');
    await expect(page.locator('text=IV League').first()).toBeVisible();
  });

  test('shows Login nav link', async ({ page }) => {
    await page.goto('/');
    // frizat: two "Login" links now exist unauthenticated (top-right nav + hero CTA, since the
    // hero CTA was repurposed from "Register with Invite" to "Login" — the invite-only redesign)
    // — .first() targets the top-right nav one this test is actually about; either resolves to
    // the same /account/login destination.
    await expect(page.getByRole('link', { name: /login/i }).first()).toBeVisible();
  });

  test('Login link navigates to login', async ({ page }) => {
    await page.goto('/');
    await page.getByRole('link', { name: 'Login' }).first().click();
    await expect(page).toHaveURL(/login/);
  });

  test('Create Account link navigates to register', async ({ page }) => {
    await page.goto('/');
    await page.getByRole('link', { name: /create account/i }).click();
    await expect(page).toHaveURL(/register/);
  });
});
