import type { LeagueUserMappingDto } from './league';

export interface LeagueInfoDto {
  id: number;
  leagueName: string;
  dateCreated: string;
  ownerUserId: string;
  leagueType: string;
}

export interface LeagueJuiceMappingDto {
  id: number;
  leagueId: number;
  leagueName: string;
  season: number;
  juice: number;
  juiceDivisional: number;
  juiceConference: number;
  weeklyCost: number;
  dateCreated: string;
}

export interface CreateLeagueModel {
  leagueName: string;
  juice: number;
  juiceDivisional: number;
  juiceConference: number;
  season: number;
  weeklyCost: number;
}

export interface UserSummaryDto {
  id: string;
  userName?: string | null;
  email?: string | null;
  emailConfirmed: boolean;
  isAdmin: boolean;
}

export interface JobStatusResponse {
  jobName: string;
  description: string;
  status: string;
  nextRun?: string | null;
  lastSucceededUtc?: string | null;
  lastFailedUtc?: string | null;
  lastMessage?: string | null;
}

export interface InvitationDto {
  id: number;
  invitationCode: string;
  email: string;
  invitedByUserId?: string | null;
  invitedByUserName?: string | null;
  createdAt: string;
  expiresAt?: string | null;
  isUsed: boolean;
  usedAt?: string | null;
  registeredUserId?: string | null;
  registeredUserName?: string | null;
  isExpired: boolean;
  isValid: boolean;
  leagueId?: number | null;
  leagueName?: string | null;
  isLeagueOwner: boolean;
}

export interface EmailRequest {
  toEmail: string;
  subject: string;
  htmlBody: string;
}

export interface LeagueCostDto {
  memberCount: number;
  cost: number;
}

export interface LeagueJuiceUpdateDto {
  juice: number;
  juiceDivisional: number;
  juiceConference: number;
  weeklyCost: number;
}

export interface LeagueCreateDto {
  leagueName: string;
  leagueType: string;
  ownerUserId: string;
  season: number;
  juice: number;
  juiceDivisional: number;
  juiceConference: number;
  weeklyCost: number;
}

export interface CfbSeasonWeekConfigDto {
  espnWeekNumber: number;
  ivLeagueWeekNumber: number;
  weekType: string;
  scoringFormat: string;
  inScopeIvLeague: boolean;
  weekStartDate: string;
  weekEndDate: string;
  notes?: string | null;
}

export type { LeagueUserMappingDto };
