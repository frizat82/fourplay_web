import { test, expect } from '@playwright/test';

test.describe('Login page', () => {
  test('renders login form', async ({ page }) => {
    await page.goto('/account/login');
    await expect(page.getByLabel('Email')).toBeVisible();
    await expect(page.getByLabel('Password')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Login' })).toBeVisible();
  });

  test('shows error toast on invalid credentials', async ({ page }) => {
    await page.goto('/account/login');
    await page.getByLabel('Email').fill('notauser@example.com');
    await page.getByLabel('Password').fill('WrongPassword1!');
    await page.getByRole('button', { name: 'Login' }).click();
    await expect(page.getByRole('alert')).toContainText(/invalid credentials/i, { timeout: 5000 });
  });

  test('forgot password button is present', async ({ page }) => {
    await page.goto('/account/login');
    await expect(page.getByRole('button', { name: /forgot your password/i })).toBeVisible();
  });

  test('register button is present', async ({ page }) => {
    await page.goto('/account/login');
    await expect(page.getByRole('button', { name: /need an account/i })).toBeVisible();
  });
});

test.describe('Register page', () => {
  test('renders registration form with invitation code field', async ({ page }) => {
    await page.goto('/account/register');
    await expect(page.getByLabel('Invitation Code')).toBeVisible();
    await expect(page.getByLabel('Username')).toBeVisible();
    await expect(page.getByLabel('Email')).toBeVisible();
    await expect(page.getByLabel('Password', { exact: true })).toBeVisible();
    await expect(page.getByLabel('Confirm Password')).toBeVisible();
  });

  test('shows invite-only notice', async ({ page }) => {
    await page.goto('/account/register');
    await expect(page.getByText(/invite-only/i)).toBeVisible();
  });

  test('shows validation errors on empty submit', async ({ page }) => {
    await page.goto('/account/register');
    await page.getByRole('button', { name: 'Register' }).click();
    await expect(page.locator('[class*="Mui-error"], [role="alert"]').first()).toBeVisible({ timeout: 3000 });
  });
});

test.describe('Forgot password page', () => {
  test('renders email field and reset button', async ({ page }) => {
    await page.goto('/account/forgotpassword');
    await expect(page.getByLabel('Email')).toBeVisible();
    await expect(page.getByRole('button', { name: /reset password/i })).toBeVisible();
  });
});
