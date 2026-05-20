/**
 * Setup: authenticate Alice on the CFB site (cfb.localhost:5174) and save cookies.
 * Runs once before all CFB demo tests via playwright.config.ts project dependency.
 */
import { test as setup, expect } from '@playwright/test';
import { DEMO_USERS } from '../helpers/demoAuth';

const authFile = 'e2e/demo/.auth/alice-cfb.json';

setup('alice — CFB auth', async ({ page }) => {
  await page.goto('/account/login');
  await page.getByLabel('Email').fill(DEMO_USERS.alice.email);
  await page.getByLabel('Password').fill(DEMO_USERS.alice.password);
  const [response] = await Promise.all([
    page.waitForResponse(res => res.url().includes('/api/auth/login') && res.status() === 200),
    page.getByRole('button', { name: 'Login' }).click(),
  ]);
  expect(response.status()).toBe(200);
  await page.waitForURL(url => !url.pathname.startsWith('/account/'), { timeout: 15_000 });
  await page.context().storageState({ path: authFile });
});
