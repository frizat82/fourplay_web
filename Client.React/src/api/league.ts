import { http } from './http';
import type {
  BatchSpreadCalculationRequest,
  BatchSpreadCalculationResponse,
  BatchSpreadRequest,
  BatchSpreadResponse,
  NflPickDto,
} from '../types/picks';
import type { LeagueUserMappingDto, NflWeekDto, NflCurrentWeekDto, SpreadLockWeekDto } from '../types/league';
import type {
  LeagueInfoDto,
  LeagueJuiceMappingDto,
  LeagueCostDto,
  AdminLeagueCostDto,
  LeagueJuiceUpdateDto,
  LeagueCreateDto,
  UserSummaryDto,
} from '../types/admin';

export async function getLeagueUserMappingsForUser(userId: string) {
  const { data } = await http.get<LeagueUserMappingDto[]>(
    `/api/league/user-mappings/by-user/${encodeURIComponent(userId)}`
  );
  return data;
}

export async function getLeagueUserMappings(leagueId: number) {
  const { data } = await http.get<LeagueUserMappingDto[]>(`/api/league/${leagueId}/users`);
  return data;
}

export async function getLeagueJuice(leagueId: number) {
  const { data } = await http.get<LeagueJuiceMappingDto[]>(`/api/league/${leagueId}/juice`);
  return data;
}

export async function getUsers() {
  const { data } = await http.get<UserSummaryDto[]>(`/api/league/users`);
  return data;
}

export async function addLeagueUserMapping(leagueId: number, userId: string) {
  await http.post('/api/league/league-user-mapping', { leagueId, userId });
}

export async function addLeagueInfo(info: LeagueInfoDto) {
  await http.post('/api/league/league-info', info);
}

export async function leagueExists(leagueName: string, season?: number) {
  if (season !== undefined) {
    const { data } = await http.get<boolean>(`/api/league/exists/league/${encodeURIComponent(leagueName)}/${season}`);
    return data;
  }
  const { data } = await http.get<boolean>(`/api/league/exists/league/${encodeURIComponent(leagueName)}`);
  return data;
}

export async function getNflWeeks(season: number) {
  const { data } = await http.get<NflWeekDto[]>(`/api/league/weeks/${season}`);
  return data ?? [];
}

export async function getNflCurrentWeek() {
  const { data } = await http.get<NflCurrentWeekDto>('/api/league/current-week');
  return data;
}

export async function getNflSpreadLockSchedule() {
  const { data } = await http.get<SpreadLockWeekDto[]>('/api/league/spread-lock-schedule');
  return data ?? [];
}

export async function getLeagueByName(leagueName: string) {
  const { data } = await http.get<LeagueInfoDto | null>(`/api/league/by-name/${encodeURIComponent(leagueName)}`);
  return data;
}

export async function addLeagueJuiceMapping(mapping: LeagueJuiceMappingDto) {
  await http.post('/api/league/league-juice-mapping', mapping);
}

export async function doOddsExist(leagueId: number, season: number, week: number) {
  const { data } = await http.get<boolean>(`/api/league/${leagueId}/odds/${season}/${week}/exists`);
  return data;
}

export async function getLeaguePicks(leagueId: number, season: number, week: number) {
  const { data } = await http.get<NflPickDto[]>(`/api/league/${leagueId}/picks/${season}/${week}`);
  return data;
}

export async function getUserPicks(userId: string, leagueId: number, season: number, week: number) {
  const { data } = await http.get<NflPickDto[]>(`/api/league/${leagueId}/picks/${season}/${week}/user/${userId}`);
  return data;
}

export async function addPicks(picks: NflPickDto[]) {
  const { data } = await http.post<number>('/api/league/picks', picks);
  return data;
}

export async function spreadBatch(
  leagueId: number,
  season: number,
  week: number,
  request: BatchSpreadRequest
) {
  const { data } = await http.post<BatchSpreadResponse>(
    `/api/league/${leagueId}/odds/${season}/${week}`,
    request
  );
  return data;
}

export async function calculateSpreadBatch(
  leagueId: number,
  season: number,
  week: number,
  request: BatchSpreadCalculationRequest
) {
  const { data } = await http.post<BatchSpreadCalculationResponse>(
    `/api/league/${leagueId}/odds/${season}/${week}/calculate-batch`,
    request
  );
  return data;
}

// Commissioner portal API

export async function getMyLeagues() {
  const { data } = await http.get<LeagueInfoDto[]>('/api/league/my-leagues');
  return data ?? [];
}

export async function getAllLeagues() {
  const { data } = await http.get<LeagueInfoDto[]>('/api/league/all-leagues');
  return data ?? [];
}

export async function getLeagueCost(leagueId: number) {
  const { data } = await http.get<LeagueCostDto>(`/api/league/${leagueId}/cost`);
  return data;
}

export async function getAllLeaguesCost(season: number) {
  const { data } = await http.get<AdminLeagueCostDto[]>('/api/league/all-leagues-cost', { params: { season } });
  return data ?? [];
}

export async function updateLeagueJuice(leagueId: number, season: number, dto: LeagueJuiceUpdateDto) {
  await http.put(`/api/league/${leagueId}/juice/${season}`, dto);
}

export async function rollForwardJuice(leagueId: number, toSeason: number) {
  await http.post(`/api/league/${leagueId}/juice/roll-forward/${toSeason}`, {});
}

export async function removeLeagueMember(leagueId: number, userId: string) {
  await http.delete(`/api/league/${leagueId}/members/${encodeURIComponent(userId)}`);
}

export async function deleteLeague(leagueId: number) {
  await http.delete(`/api/league/${leagueId}`);
}

export type LeagueInviteOutcome = 'NewUserInvitationSent' | 'ExistingUserInvitePending';

export interface LeagueInviteResultDto {
  email: string;
  outcome: LeagueInviteOutcome;
}

export async function inviteToLeague(leagueId: number, email: string): Promise<LeagueInviteResultDto> {
  const { data } = await http.post<LeagueInviteResultDto>(`/api/league/${leagueId}/invite`, { email, baseUrl: window.location.origin });
  return data;
}

export async function assignLeagueOwner(leagueId: number, newOwnerUserId: string) {
  await http.put(`/api/league/${leagueId}/owner/${encodeURIComponent(newOwnerUserId)}`, {});
}

export async function createLeague(dto: LeagueCreateDto) {
  const { data } = await http.post<LeagueInfoDto>('/api/league/create', dto);
  return data;
}

export interface LeagueInviteLinkDto {
  token: string;
  leagueId: number;
  leagueName: string;
  expiresAt: string;
}

export async function generateInviteLink(leagueId: number): Promise<LeagueInviteLinkDto> {
  const { data } = await http.post<LeagueInviteLinkDto>(`/api/league/${leagueId}/invite-link`, {});
  return data;
}

export async function validateInviteLink(token: string): Promise<LeagueInviteLinkDto | null> {
  try {
    const { data } = await http.get<LeagueInviteLinkDto>(`/api/league/join/${token}`);
    return data;
  } catch (err) {
    const status = (err as { response?: { status?: number } }).response?.status;
    if (status === 404) return null;
    throw err;
  }
}

export async function joinViaLink(token: string): Promise<void> {
  await http.post(`/api/league/join/${token}`, {});
}

export interface InvitationDto {
  id: number;
  email: string;
  createdAt: string;
  expiresAt: string | null;
  isUsed: boolean;
  isExpired: boolean;
  isValid: boolean;
  usedAt: string | null;
  registeredUserEmailConfirmed?: boolean | null;
}

export async function getCurrentInviteLink(leagueId: number): Promise<LeagueInviteLinkDto | null> {
  try {
    const { data } = await http.get<LeagueInviteLinkDto>(`/api/league/${leagueId}/invite-link`);
    return data;
  } catch (err) {
    const status = (err as { response?: { status?: number } }).response?.status;
    if (status === 404) return null;
    throw err;
  }
}

export async function getLeagueInvitations(leagueId: number): Promise<InvitationDto[]> {
  const { data } = await http.get<InvitationDto[]>(`/api/league/${leagueId}/invitations`);
  return data;
}

export async function revokeInviteLink(leagueId: number): Promise<void> {
  await http.delete(`/api/league/${leagueId}/invite-link`);
}

export interface PendingMembershipInviteDto {
  id: number;
  leagueId: number;
  leagueName: string;
  invitedByUserName: string | null;
  createdAt: string;
}

export async function getMyPendingMembershipInvites(): Promise<PendingMembershipInviteDto[]> {
  const { data } = await http.get<PendingMembershipInviteDto[]>('/api/league/membership-invites/mine');
  return data;
}

export async function acceptMembershipInvite(id: number): Promise<void> {
  await http.post(`/api/league/membership-invites/${id}/accept`, {});
}

export async function declineMembershipInvite(id: number): Promise<void> {
  await http.post(`/api/league/membership-invites/${id}/decline`, {});
}

export async function cancelMembershipInvite(id: number): Promise<void> {
  await http.delete(`/api/league/membership-invites/${id}`);
}

export type MembershipInviteStatus = 'Pending' | 'Accepted' | 'Declined';

export interface MembershipInviteStatusDto {
  id: number;
  leagueId: number;
  invitedUserEmail: string;
  invitedUserName: string | null;
  status: MembershipInviteStatus;
  createdAt: string;
  respondedAt: string | null;
}

export async function getLeagueMembershipInvites(leagueId: number): Promise<MembershipInviteStatusDto[]> {
  const { data } = await http.get<MembershipInviteStatusDto[]>(`/api/league/${leagueId}/membership-invites`);
  return data;
}
