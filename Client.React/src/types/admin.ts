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

export interface MapUserLeagueDto {
  leagueId: number;
  userId: string;
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
  lastRun?: string | null;
  lastStartedUtc?: string | null;
  runCount: number;
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
}

export interface EmailRequest {
  toEmail: string;
  subject: string;
  htmlBody: string;
}

export type { LeagueUserMappingDto };
