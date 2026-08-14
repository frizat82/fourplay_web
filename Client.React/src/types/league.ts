export type LeagueType = 0 | 1; // 0 = NFL, 1 = CFB

export interface LeagueUserMappingDto {
  id: number;
  leagueId: number;
  leagueOwnerUserId?: string | null;
  userId: string;
  userName?: string | null;
  email?: string | null;
  leagueName?: string | null;
  leagueType: LeagueType;
  dateCreated: string;
}

export interface CfbSlateDto {
  id: number;
  season: number;
  slateNumber: number;
  label: string;
  slateType: string;
  startDate: string;
  endDate: string;
  firstGameUtc?: string | null;
}

export interface CfbSpreadDto {
  id: number;
  cfbSlateId: number;
  homeTeam: string;
  awayTeam: string;
  homeTeamSpread: number;
  awayTeamSpread: number;
  overUnder: number;
  gameTime: string;
  dateCreated: string;
}

export interface CfbScoreDto {
  id: number;
  cfbSlateId: number;
  homeTeam: string;
  awayTeam: string;
  homeTeamScore: number;
  awayTeamScore: number;
  gameStatus: string;
  gameTime: string;
  weatherDisplayValue?: string | null;
  weatherConditionId?: string | null;
  weatherTemperatureF?: number | null;
}

export interface CfbPickDto {
  id: number;
  userId: string;
  userName: string;
  leagueId: number;
  cfbSlateId: number;
  team: string;
  pickType: string;
  season: number;
}

export interface NflWeekDto {
  id: number;
  nflWeek: number;
  season: number;
  startDate: string;
  endDate: string;
  dateCreated: string;
}

// GET /api/league/current-week — mirrors CfbSlateDto's role for NFL: the season/week to treat
// as "current," resolved server-side with a real off-season/pre-season fallback (NflCurrentWeekService).
export interface NflCurrentWeekDto {
  weekId: number;
  espnWeek: number;
  season: number;
  isPostSeason: boolean;
  weekLabel: string;
  scoringFormat: string;
  spreadLockDatetime: string;
}

// GET /api/league/spread-lock-schedule (NFL) / /api/cfb/spread-lock-schedule (CFB) — Rules page:
// the full current season's spread-lock schedule, one row per in-scope week, shared shape for
// both sports.
export interface SpreadLockWeekDto {
  weekLabel: string;
  spreadLockDatetime: string;
}
