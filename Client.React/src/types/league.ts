export interface LeagueUserMappingDto {
  id: number;
  leagueId: number;
  leagueOwnerUserId?: string | null;
  userId: string;
  userName?: string | null;
  leagueName?: string | null;
  dateCreated: string;
}

export interface NflWeekDto {
  id: number;
  nflWeek: number;
  season: number;
  startDate: string;
  endDate: string;
  dateCreated: string;
}
