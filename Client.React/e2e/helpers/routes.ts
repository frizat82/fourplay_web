import type { Page } from '@playwright/test';
import type { UserInfo } from '../../src/types/auth';
import type { NflPickDto } from '../../src/types/picks';
import type { LeagueUserMappingDto } from '../../src/types/league';
import type { LeaderboardDto } from '../../src/types/leaderboard';
import { createScores, createSpreadResponse } from '../../src/test/fixtures';

export const TEST_USER: UserInfo = {
  userId: 'test-user-id-001',
  name: 'TestUser',
  claims: [
    { type: 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier', value: 'test-user-id-001' },
    { type: 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name', value: 'TestUser' },
  ],
};

export const ADMIN_USER: UserInfo = {
  userId: 'admin-user-id-001',
  name: 'AdminUser',
  claims: [
    { type: 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier', value: 'admin-user-id-001' },
    { type: 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name', value: 'AdminUser' },
    { type: 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role', value: 'Administrator' },
  ],
};

const TEST_LEAGUE_ID = 1;
const TEST_SEASON = 2024;
const TEST_WEEK = 2;

export interface SetupRoutesOptions {
  /** User's own picks (for picks page) */
  userPicks?: NflPickDto[];
  /** All league picks (for scores page) */
  leaguePicks?: NflPickDto[];
  /** Leaderboard rows (for leaderboard page) */
  leaderboard?: LeaderboardDto[];
  isPostSeason?: boolean;
  week?: number;
  season?: number;
  /**
   * Whether games are already started in the ESPN scores response.
   * - false (default): games are in the future — Pick buttons are enabled
   * - true: games are finished — score badges become visible
   */
  gameStarted?: boolean;
  /** Override the authenticated user returned by /api/auth/me (default: TEST_USER) */
  authUser?: UserInfo;
}

/**
 * Installs a single catch-all route interceptor for all /api/ requests.
 * Using a single handler avoids Playwright's route ordering/precedence issues
 * and makes it easy to route based on URL + method without confusion.
 */
export async function setupRoutes(page: Page, options: SetupRoutesOptions = {}): Promise<void> {
  const {
    userPicks = [],
    leaguePicks = [],
    leaderboard = [],
    isPostSeason = false,
    week = TEST_WEEK,
    season = TEST_SEASON,
    gameStarted = false,
    authUser = TEST_USER,
  } = options;

  const bufSpread = createSpreadResponse('BUF', -3.5, 48.5, 48.5);
  const miaSpread = createSpreadResponse('MIA', 3.5, 48.5, 48.5);
  const dalSpread = createSpreadResponse('DAL', -6.5, 51.0, 51.0);
  const nygSpread = createSpreadResponse('NYG', 6.5, 51.0, 51.0);

  const league: LeagueUserMappingDto = {
    id: 1,
    leagueId: TEST_LEAGUE_ID,
    userId: TEST_USER.userId,
    userName: TEST_USER.name,
    leagueName: 'Test League',
    leagueOwnerUserId: TEST_USER.userId,
    dateCreated: new Date().toISOString(),
  };

  const scoresData = createScores({ week, seasonYear: season, postSeason: isPostSeason, gameStarted });

  await page.route('**/*', (route) => {
    const url = route.request().url();
    const method = route.request().method();

    // Only intercept /api/ requests — let all other requests (JS, CSS, HTML) pass through
    if (!url.includes('/api/')) {
      void route.continue();
      return;
    }

    // ── Auth ────────────────────────────────────────────────────────────────
    if (url.includes('/api/auth/me') && method === 'GET') {
      void route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(authUser) });
      return;
    }

    // ── Auth logout/login — let fail gracefully (not needed in happy path tests) ──
    if (url.includes('/api/auth/')) {
      void route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({}) });
      return;
    }

    // ── League user mappings ─────────────────────────────────────────────────
    if (url.includes('/api/league/user-mappings/by-user/') && method === 'GET') {
      void route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([league]) });
      return;
    }

    // ── POST /api/league/picks — submit picks ───────────────────────────────
    // Must check before the broad picks GET handler below.
    if (url.match(/\/api\/league\/picks$/) && method === 'POST') {
      void route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(1) });
      return;
    }

    // ── Odds exists ──────────────────────────────────────────────────────────
    if (url.includes('/api/league/') && url.includes('/odds/') && url.endsWith('/exists') && method === 'GET') {
      void route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(true) });
      return;
    }

    // ── POST /api/league/*/odds/*/*/calculate-batch — scores page ───────────
    if (url.includes('/api/league/') && url.includes('/odds/') && url.includes('/calculate-batch') && method === 'POST') {
      void route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          results: {
            BUF: { ...bufSpread, isWinner: false, isOverWinner: false, isUnderWinner: false },
            MIA: { ...miaSpread, isWinner: false, isOverWinner: false, isUnderWinner: false },
            DAL: { ...dalSpread, isWinner: false, isOverWinner: false, isUnderWinner: false },
            NYG: { ...nygSpread, isWinner: false, isOverWinner: false, isUnderWinner: false },
          },
        }),
      });
      return;
    }

    // ── POST /api/league/*/odds/*/* — picks page spread batch ───────────────
    if (url.includes('/api/league/') && url.includes('/odds/') && method === 'POST') {
      void route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          responses: { BUF: bufSpread, MIA: miaSpread, DAL: dalSpread, NYG: nygSpread },
        }),
      });
      return;
    }

    // ── User-specific picks: GET /api/league/<id>/picks/<s>/<w>/user/<uid> ──
    if (url.includes('/api/league/') && url.includes('/picks/') && url.includes('/user/') && method === 'GET') {
      void route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(userPicks) });
      return;
    }

    // ── League picks: GET /api/league/<id>/picks/<season>/<week> ───────────
    if (url.includes('/api/league/') && url.includes('/picks/') && method === 'GET') {
      void route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(leaguePicks) });
      return;
    }

    // ── ESPN scores/week — historical ──────────────────────────────────────
    if (url.includes('/api/espn/scores/week/') && method === 'GET') {
      void route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(scoresData) });
      return;
    }

    // ── ESPN scores — current week ─────────────────────────────────────────
    if (url.includes('/api/espn/scores') && method === 'GET') {
      void route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(scoresData) });
      return;
    }

    // ── Jerseys ────────────────────────────────────────────────────────────
    if (url.includes('/api/jerseys/') && method === 'GET') {
      void route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({}) });
      return;
    }

    // ── Leaderboard ────────────────────────────────────────────────────────
    if (url.includes('/api/leaderboard/') && method === 'GET') {
      void route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(leaderboard) });
      return;
    }

    // ── Misc league endpoints ──────────────────────────────────────────────
    if (url.includes('/api/league/leagues') && method === 'GET') {
      void route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([{ leagueId: TEST_LEAGUE_ID, leagueName: 'Test League' }]),
      });
      return;
    }

    // ── Admin: users list ──────────────────────────────────────────────────
    if (url.includes('/api/league/users') && method === 'GET') {
      void route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'u1', userName: 'alice', email: 'alice@example.com', emailConfirmed: true, isAdmin: false },
          { id: 'u2', userName: 'bob', email: 'bob@example.com', emailConfirmed: false, isAdmin: false },
        ]),
      });
      return;
    }

    // ── Admin: confirm email / assign role / delete user ───────────────────
    if (
      (url.includes('/api/auth/admin-confirm-email/') ||
        url.includes('/api/auth/assign-user-role') ||
        url.includes('/api/auth/delete-user/')) &&
      (method === 'GET' || method === 'POST')
    ) {
      void route.fulfill({ status: 200, contentType: 'application/json', body: 'true' });
      return;
    }

    // ── Admin: invitations ─────────────────────────────────────────────────
    if (url.includes('/api/invitations/all') && method === 'GET') {
      void route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: 1,
            invitationCode: 'code-abc123',
            email: 'invite@example.com',
            invitedByUserId: 'admin-user-id-001',
            invitedByUserName: 'AdminUser',
            createdAt: new Date().toISOString(),
            expiresAt: null,
            isUsed: false,
            usedAt: null,
            registeredUserId: null,
            registeredUserName: null,
            isExpired: false,
            isValid: true,
          },
        ]),
      });
      return;
    }

    if (url.includes('/api/invitations') && (method === 'POST' || method === 'DELETE')) {
      void route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({}) });
      return;
    }

    // ── Admin: job manager ─────────────────────────────────────────────────
    if (url.includes('/api/jobmanager/get-jobs') && method === 'GET') {
      void route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            jobName: 'NflScoresJob',
            description: 'Refreshes NFL scores',
            status: 'Idle',
            nextRun: null,
            lastRun: null,
            lastStartedUtc: null,
            runCount: 0,
            lastMessage: null,
          },
        ]),
      });
      return;
    }

    if (url.includes('/api/jobmanager/') && method === 'POST') {
      void route.fulfill({ status: 200 });
      return;
    }

    // ── Admin: league management (existing endpoints cover most; add extras) ─
    if (url.includes('/api/league/juice/') && method === 'GET') {
      void route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([]),
      });
      return;
    }

    if (url.includes('/api/league/exists') && method === 'GET') {
      void route.fulfill({ status: 200, contentType: 'application/json', body: 'false' });
      return;
    }

    // Pass through anything else (shouldn't happen in happy path)
    void route.continue();
  });
}
