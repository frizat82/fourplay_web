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
  // The slate's control-table rules, copied from CfbSeasonWeekConfigs (frizat-d47).
  isPostSeason: boolean;
  requiredPicks: number;
}

export interface CfbSpreadDto {
  id: number;
  cfbSlateId: number;
  homeTeam: string;
  awayTeam: string;
  homeTeamSpread: number;
  awayTeamSpread: number;
  // Over and Under are independently juiced and NOT the same number once juice is nonzero —
  // same reasoning as homeTeamSpread/awayTeamSpread above.
  over: number;
  under: number;
  gameTime: string;
  dateCreated: string;
  // AP Top 25 rank (1-25), null when unranked. NFL has no equivalent (no polls).
  homeTeamRank: number | null;
  awayTeamRank: number | null;
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

/** Submitted pick count for one member — a count only, never which team. Mirrors the server's MemberPickCountDto. */
export interface MemberPickCountDto {
  userId: string;
  pickCount: number;
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
// No espnWeek field — nothing on the frontend routes by ESPN's own week numbering anymore,
// only weekId (frizat-3nv).
export interface NflCurrentWeekDto {
  weekId: number;
  season: number;
  isPostSeason: boolean;
  weekLabel: string;
  scoringFormat: string;
  spreadLockDatetime: string;
  requiredPicks: number; // the week's control-table row (frizat-d47)
}

// GET /api/league/season-weeks/{season} — one NFL week as its control-table row defines it
// (frizat-d47). nflAdapter reads a past week's requiredPicks from here; the label and postseason
// flag are the row's too, for building the week selector from the table rather than constants.
export interface NflSeasonWeekDto {
  weekId: number;
  weekLabel: string;
  isPostSeason: boolean;
  requiredPicks: number;
}

// GET /api/league/spread-lock-schedule (NFL) / /api/cfb/spread-lock-schedule (CFB) — Rules page:
// the full current season's spread-lock schedule, one row per in-scope week, shared shape for
// both sports.
export interface SpreadLockWeekDto {
  weekLabel: string;
  spreadLockDatetime: string;
  // The row's control-table rules (frizat-d47) — the Rules page's per-round pick counts.
  isPostSeason: boolean;
  requiredPicks: number;
}
