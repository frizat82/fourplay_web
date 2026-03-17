import { test, expect } from '@playwright/test';

test.describe('Home page', () => {
  test('renders FourPlay hero section', async ({ page }) => {
    await page.goto('/');
    await expect(page.locator('text=FOURPLAY').first()).toBeVisible();
    await expect(page.locator('text=Elevate Your Fantasy Game')).toBeVisible();
  });

  test('shows Login and Register nav links', async ({ page }) => {
    await page.goto('/');
    await expect(page.getByRole('link', { name: /login/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /register/i }).or(page.getByRole('link', { name: /register/i }))).toBeVisible();
  });

  test('Log In to Start link navigates to login', async ({ page }) => {
    await page.goto('/');
    await page.getByRole('link', { name: /log in to start/i }).click();
    await expect(page).toHaveURL(/login/);
  });

  test('Create Account link navigates to register', async ({ page }) => {
    await page.goto('/');
    await page.getByRole('link', { name: /create account/i }).click();
    await expect(page).toHaveURL(/register/);
  });
});
