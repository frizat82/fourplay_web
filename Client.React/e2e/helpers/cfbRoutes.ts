import type { Page } from '@playwright/test';
import type { UserInfo } from '../../src/types/auth';
import type { CfbPickDto, CfbSlateDto, CfbSpreadDto, LeagueUserMappingDto } from '../../src/types/league';
import type { LeaderboardDto } from '../../src/types/leaderboard';
import type { EspnScores } from '../../src/types/espn';
import { createCompetition } from '../../src/test/fixtures';
import { TEST_USER } from './routes';
import { FAKE_JWT } from './auth';

const mockInviteLink = () => ({
  token: 'mocktokenabcdef1234567890abcdef12',
  leagueId: 199,
  leagueName: 'Test CFB League',
  expiresAt: new Date(Date.now() + 86400000).toISOString(),
});

export const TEST_LEAGUE_ID = 2;
export const TEST_SEASON = 2024;
// Regular-season slate 1 → cfbSlateNumberToWeek gives week 1, non-postseason,
// 4 required picks (getCfbRequiredPicks: slateNumber <= 14 => 4) — same shape as picks.spec.ts's
// 2-games/4-buttons NFL setup.
export const TEST_SLATE_ID = 501;
export const TEST_SLATE_NUMBER = 1;

export function createCfbPick(overrides: Partial<CfbPickDto> & { team: string }): CfbPickDto {
  return {
    id: 0,
    userId: '123',
    userName: 'TestUser',
    leagueId: TEST_LEAGUE_ID,
    cfbSlateId: TEST_SLATE_ID,
    pickType: 'Spread',
    season: TEST_SEASON,
    ...overrides,
  };
}

function cfbSpread(
  cfbSlateId: number,
  homeTeam: string,
  awayTeam: string,
  homeTeamSpread: number,
  overUnder: number,
  homeTeamRank: number | null = null,
  awayTeamRank: number | null = null
): CfbSpreadDto {
  return {
    id: cfbSlateId * 10 + homeTeam.length,
    cfbSlateId,
    homeTeam,
    awayTeam,
    homeTeamSpread,
    awayTeamSpread: -homeTeamSpread,
    overUnder,
    gameTime: new Date(Date.now() + 2 * 60 * 60 * 1000).toISOString(),
    dateCreated: new Date().toISOString(),
    homeTeamRank,
    awayTeamRank,
  };
}

export interface SetupCfbRoutesOptions {
  userPicks?: CfbPickDto[];
  leaguePicks?: CfbPickDto[];
  leaderboard?: LeaderboardDto[];
  gameStarted?: boolean;
  authUser?: UserInfo;
}

/**
 * CFB counterpart to routes.ts's setupRoutes — mocks the DB-backed /api/cfb/* endpoints
 * (spreads/picks/slate metadata) plus the ESPN live-overlay endpoints
 * (/api/espn/cfb/*), matching cfbAdapter.ts's actual call graph. Intended for use with a
 * cfb.localhost baseURL (see mockCfbAuth) so the app resolves the CFB adapter via
 * useSportContext's hostname check.
 */
export async function setupCfbRoutes(page: Page, options: SetupCfbRoutesOptions = {}): Promise<void> {
  const { userPicks = [], leaguePicks = [], leaderboard = [], gameStarted = false, authUser = TEST_USER } = options;

  const slate: CfbSlateDto = {
    id: TEST_SLATE_ID,
    season: TEST_SEASON,
    slateNumber: TEST_SLATE_NUMBER,
    label: 'Week 1',
    slateType: 'RegularSeason',
    startDate: new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString(),
    endDate: new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString(),
    firstGameUtc: new Date(Date.now() + 2 * 60 * 60 * 1000).toISOString(),
  };

  const spreads: CfbSpreadDto[] = [
    cfbSpread(TEST_SLATE_ID, 'OSU', 'MICH', -6.5, 51.5, 3, null),
    cfbSpread(TEST_SLATE_ID, 'ALA', 'UGA', -3.5, 48.5),
  ];

  const league: LeagueUserMappingDto = {
    id: 1,
    leagueId: TEST_LEAGUE_ID,
    userId: authUser.userId,
    userName: authUser.name,
    leagueName: 'Test CFB League',
    leagueOwnerUserId: authUser.userId,
    leagueType: 1, // 1 = CFB — satisfies hasCfbAccess check
    dateCreated: new Date().toISOString(),
  };

  const scoresData: EspnScores = {
    season: { year: TEST_SEASON, type: 2 },
    week: { number: TEST_SLATE_NUMBER },
    events: [
      {
        id: '1',
        season: { year: TEST_SEASON, type: 2 },
        week: { number: TEST_SLATE_NUMBER },
        date: new Date().toISOString(),
        competitions: [createCompetition({ homeTeam: 'OSU', awayTeam: 'MICH', homeScore: 24, awayScore: 17, gameStarted })],
      },
      {
        id: '2',
        season: { year: TEST_SEASON, type: 2 },
        week: { number: TEST_SLATE_NUMBER },
        date: new Date().toISOString(),
        competitions: [createCompetition({ homeTeam: 'ALA', awayTeam: 'UGA', homeScore: 28, awayScore: 14, gameStarted })],
      },
    ],
  };

  await page.route('**/*', (route) => {
    const url = route.request().url();
    const method = route.request().method();

    if (!url.includes('/api/')) {
      void route.continue();
      return;
    }

    // ── Auth ────────────────────────────────────────────────────────────────
    if (url.includes('/api/auth/me') && method === 'GET') {
      void route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(authUser) });
      return;
    }
    if (url.includes('/api/auth/')) {
      void route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({}) });
      return;
    }

    // ── Session hydration ────────────────────────────────────────────────────
    if (url.includes('/api/league/user-mappings/by-user/') && method === 'GET') {
      void route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([league]) });
      return;
    }
    if (url.includes('/api/league/my-leagues') && method === 'GET') {
      void route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([{ id: TEST_LEAGUE_ID, leagueName: 'Test CFB League', leagueType: 'Cfb', ownerUserId: authUser.userId, dateCreated: new Date().toISOString() }]),
      });
      return;
    }
    if (url.includes('/api/league/membership-invites/mine') && method === 'GET') {
      void route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) });
      return;
    }

    // ── CFB slate/spread/score/pick endpoints (own DB, per cfbAdapter.ts) ────
    if (url.includes('/api/cfb/current-slate') && method === 'GET') {
      void route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(slate) });
      return;
    }
    if (url.match(/\/api\/cfb\/slates\/\d+$/) && method === 'GET') {
      void route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([slate]) });
      return;
    }
    if (url.match(/\/api\/cfb\/spreads\/\d+\/\d+$/) && method === 'GET') {
      void route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(spreads) });
      return;
    }
    if (url.match(/\/api\/cfb\/scores\/\d+$/) && method === 'GET') {
      void route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) });
      return;
    }
    // Must check before the broader picks GET handler below.
    if (url.match(/\/api\/cfb\/picks$/) && method === 'POST') {
      void route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ added: 1 }) });
      return;
    }
    if (url.match(/\/api\/cfb\/picks\/\d+\/\d+\/user$/) && method === 'GET') {
      void route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(userPicks) });
      return;
    }
    if (url.match(/\/api\/cfb\/picks\/\d+\/\d+$/) && method === 'GET') {
      void route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(leaguePicks) });
      return;
    }
    if (url.match(/\/api\/cfb\/picks\/\d+\/\d+$/) && method === 'DELETE') {
      void route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({}) });
      return;
    }
    if (url.includes('/api/cfb/live-stream') && method === 'GET') {
      void route.fulfill({ status: 200, contentType: 'text/event-stream', body: '' });
      return;
    }

    // ── ESPN live-overlay endpoints (per src/api/espn.ts) ────────────────────
    if (url.includes('/api/espn/cfb/livegames') && method === 'GET') {
      void route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) });
      return;
    }
    if (url.includes('/api/espn/cfb/scores/slate/') && method === 'GET') {
      void route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(scoresData) });
      return;
    }
    if (url.includes('/api/espn/cfb/scores') && method === 'GET') {
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

    // useLeagueMinSeason (Picks/Scores/Leaderboard season selectors) reads this on every page —
    // empty history so it falls back to cfbAdapter's own sport-wide minSeason, same as before
    // that hook existed, rather than falling through to a real (nonexistent-in-CI) backend.
    if (url.match(/\/api\/league\/\d+\/juice$/) && method === 'GET') {
      void route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) });
      return;
    }

    // ── Invite link / misc league bits some layouts probe on every page ─────
    if (url.match(/\/api\/league\/\d+\/invite-link$/) && method === 'GET') {
      void route.fulfill({ status: 404 });
      return;
    }
    if (url.match(/\/api\/league\/\d+\/invite-link$/) && method === 'POST') {
      void route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(mockInviteLink()) });
      return;
    }

    void route.continue();
  });
}

export interface MockCfbAuthOptions extends SetupCfbRoutesOptions {
  navigateTo?: string;
}

/**
 * CFB counterpart to auth.ts's mockAuth. Callers must run under a cfb.localhost baseURL
 * (see playwright's per-file test.use({ baseURL }) at the top of *.cfb.spec.ts) so
 * useSportContext resolves the CFB adapter.
 */
export async function mockCfbAuth(page: Page, options: MockCfbAuthOptions = {}): Promise<void> {
  const { navigateTo, ...routeOptions } = options;

  await setupCfbRoutes(page, routeOptions);

  await page.goto('/');

  // Domain 'localhost' domain-matches the 'cfb.localhost' subdomain per RFC 6265 —
  // no separate cookie domain needed here (mirrors auth.ts's mockAuth).
  await page.context().addCookies([
    {
      name: 'AuthToken',
      value: FAKE_JWT,
      domain: 'localhost',
      path: '/',
      httpOnly: false,
      secure: false,
      sameSite: 'Lax',
    },
  ]);

  if (navigateTo) {
    await page.goto(navigateTo);
  }
}
