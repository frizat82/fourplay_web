export type LeagueType = 0 | 1; // 0 = NFL, 1 = CFB

export interface LeagueUserMappingDto {
  id: number;
  leagueId: number;
  leagueOwnerUserId?: string | null;
  userId: string;
  userName?: string | null;
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
  espnEventId: number;
  homeTeam: string;
  awayTeam: string;
  homeTeamSpread: number;
  awayTeamSpread: number;
  overUnder: number;
  gameTime: string;
}

export interface CfbScoreDto {
  id: number;
  cfbSlateId: number;
  espnEventId: number;
  homeTeam: string;
  awayTeam: string;
  homeTeamScore: number;
  awayTeamScore: number;
  gameStatus: string;
  gameTime: string;
}

export interface CfbPickDto {
  id: number;
  userId: string;
  leagueId: number;
  cfbSlateId: number;
  espnEventId: number;
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
