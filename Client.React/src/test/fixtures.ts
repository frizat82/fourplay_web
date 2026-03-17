import type { Competition, EspnScores, Event } from '../types/espn';
import type { NflPickDto, PickType, SpreadCalculationResponse, SpreadResponse } from '../types/picks';

interface CompetitionOptions {
  homeTeam: string;
  awayTeam: string;
  homeScore?: number;
  awayScore?: number;
  gameStarted?: boolean;
  date?: string;
}

export function createCompetition({
  homeTeam,
  awayTeam,
  homeScore = 21,
  awayScore = 14,
  gameStarted = true,
  date,
}: CompetitionOptions): Competition {
  // Default date: past for started games (any past time), 2h future for scheduled games
  // so isAfterKickoff() returns the expected value in tests without manual injection.
  if (date === undefined) {
    date = gameStarted
      ? new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString()
      : new Date(Date.now() + 2 * 60 * 60 * 1000).toISOString();
  }
  const statusName = gameStarted ? 'status_final' : 'status_scheduled';

  return {
    id: `${homeTeam}vs${awayTeam}`,
    date,
    competitors: [
      {
        id: `${homeTeam}-id`,
        homeAway: 'home',
        team: { abbreviation: homeTeam, logo: `https://example.com/logo_${homeTeam}.png` },
        score: homeScore,
        records: [{ name: 'overall', type: 'total', summary: '1-0' }],
      },
      {
        id: `${awayTeam}-id`,
        homeAway: 'away',
        team: { abbreviation: awayTeam, logo: `https://example.com/logo_${awayTeam}.png` },
        score: awayScore,
        records: [{ name: 'overall', type: 'total', summary: '1-0' }],
      },
    ],
    status: {
      clock: 0,
      displayClock: '0:00',
      period: gameStarted ? 4 : 0,
      type: {
        id: 1,
        name: statusName,
        state: gameStarted ? 'post' : 'pre',
        completed: gameStarted,
        description: '',
        detail: '',
        shortDetail: '',
      },
    },
    situation: {
      down: 1,
      yardLine: 25,
      distance: 10,
      downDistanceText: '1st & 10',
      shortDownDistanceText: '1st & 10',
      possessionText: '',
      homeTimeouts: 3,
      awayTimeouts: 3,
      possession: `${homeTeam}-id`,
    },
  };
}

interface ScoresOptions {
  week: number;
  seasonYear?: number;
  postSeason?: boolean;
  events?: Event[];
  gameStarted?: boolean;
}

export function createScores({
  week,
  seasonYear = 2024,
  postSeason = false,
  events,
  gameStarted = true,
}: ScoresOptions): EspnScores {
  const defaultEvents: Event[] = events ?? [
    {
      id: '1',
      season: { year: seasonYear, type: postSeason ? 3 : 2 },
      week: { number: week },
      date: new Date().toISOString(),
      competitions: [createCompetition({ homeTeam: 'BUF', awayTeam: 'MIA', gameStarted })],
    },
    {
      id: '2',
      season: { year: seasonYear, type: postSeason ? 3 : 2 },
      week: { number: week },
      date: new Date().toISOString(),
      competitions: [createCompetition({ homeTeam: 'DAL', awayTeam: 'NYG', homeScore: 28, awayScore: 17, gameStarted })],
    },
  ];

  return {
    season: { year: seasonYear, type: postSeason ? 3 : 2 },
    week: { number: week },
    events: defaultEvents,
  };
}

interface PickOptions {
  team: string;
  pick?: PickType;
  userId?: string;
  userName?: string;
  leagueId?: number;
  season?: number;
  nflWeek?: number;
}

export function createPick({
  team,
  pick = 'Spread',
  userId = '123',
  userName = 'TestUser',
  leagueId = 1,
  season = 2024,
  nflWeek = 2,
}: PickOptions): NflPickDto {
  return {
    id: 0,
    team,
    pick,
    userId,
    userName,
    leagueId,
    season,
    nflWeek,
    dateCreated: new Date().toISOString(),
  };
}

export function createSpreadResponse(team: string, spread: number, over?: number, under?: number): SpreadResponse {
  return { team, spread, over: over ?? null, under: under ?? null };
}

export function createSpreadCalculationResponse(
  team: string,
  spread: number,
  isWinner = true,
  over?: number,
  under?: number,
  isOverWinner?: boolean,
  isUnderWinner?: boolean
): SpreadCalculationResponse {
  return {
    team,
    spread,
    over: over ?? null,
    under: under ?? null,
    isWinner,
    isOverWinner: isOverWinner ?? isWinner,
    isUnderWinner: isUnderWinner ?? isWinner,
  };
}

// ── Leaderboard ────────────────────────────────────────────────────────────────
import type { LeaderboardDto, LeaderboardWeekResults, WeekResult } from '../types/leaderboard';

export function createLeaderboardWeekResult(overrides?: Partial<LeaderboardWeekResults>): LeaderboardWeekResults {
  return { week: 1, weekResult: 'Won' as WeekResult, score: 10, ...overrides };
}

export function createLeaderboardEntry(overrides?: Partial<LeaderboardDto>): LeaderboardDto {
  return {
    userId: 'u1',
    userName: 'TestUser',
    rank: '1',
    total: 10,
    weekResults: [createLeaderboardWeekResult()],
    ...overrides,
  };
}
