import { test, expect } from '@playwright/test';

test.describe('Home page', () => {
  test('renders IV League hero section', async ({ page }) => {
    await page.goto('/');
    await expect(page.locator('text=IV League').first()).toBeVisible();
  });

  test('shows Login nav link', async ({ page }) => {
    await page.goto('/');
    await expect(page.getByRole('link', { name: /login/i })).toBeVisible();
  });

  test('Login link navigates to login', async ({ page }) => {
    await page.goto('/');
    await page.getByRole('link', { name: 'Login' }).click();
    await expect(page).toHaveURL(/login/);
  });

  test('Create Account link navigates to register', async ({ page }) => {
    await page.goto('/');
    await page.getByRole('link', { name: /create account/i }).click();
    await expect(page).toHaveURL(/register/);
  });
});
