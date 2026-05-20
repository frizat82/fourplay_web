import { expect, type Page } from '@playwright/test';

export const DEMO_USERS = {
  alice:  { email: 'alice@demo.local',  password: 'DemoPass@123', name: 'alice'  },
  bob:    { email: 'bob@demo.local',    password: 'DemoPass@123', name: 'bob'    },
  carlos: { email: 'carlos@demo.local', password: 'DemoPass@123', name: 'carlos' },
  dana:   { email: 'dana@demo.local',   password: 'DemoPass@123', name: 'dana'   },
  eve:    { email: 'eve@demo.local',    password: 'DemoPass@123', name: 'eve'    },
} as const;

/**
 * Log in as a real demo user against the running demo backend.
 * After this call, the page has an authenticated session cookie.
 */
export async function demoLogin(page: Page, user: { email: string; password: string }): Promise<void> {
  await page.goto('/account/login');
  await page.getByLabel('Email').fill(user.email);
  await page.getByLabel('Password').fill(user.password);
  // Register waitForURL BEFORE clicking so we can't miss the navigation event.
  // Use waitUntil:'commit' so we resolve on URL change, not after all page resources load.
  await Promise.all([
    page.waitForURL((url) => !url.pathname.startsWith('/account/'), { timeout: 15_000, waitUntil: 'commit' }),
    page.getByRole('button', { name: 'Login' }).click(),
  ]);
}

export async function waitForSpinner(page: Page): Promise<void> {
  await expect(page.getByRole('progressbar')).not.toBeVisible({ timeout: 10_000 });
}
