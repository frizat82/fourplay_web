import { http } from './http';
import type {
  BatchSpreadCalculationRequest,
  BatchSpreadCalculationResponse,
  BatchSpreadRequest,
  BatchSpreadResponse,
  NflPickDto,
} from '../types/picks';
import type { LeagueUserMappingDto, NflWeekDto } from '../types/league';
import type { LeagueInfoDto, LeagueJuiceMappingDto, UserSummaryDto } from '../types/admin';

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

export async function addLeagueUserMapping(mapping: LeagueUserMappingDto) {
  await http.post('/api/league/league-user-mapping', mapping);
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
