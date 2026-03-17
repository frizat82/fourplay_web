import { http } from './http';
import type { LeaderboardDto } from '../types/leaderboard';

export async function getLeaderboard(leagueId: number, season: number) {
  const { data } = await http.get<LeaderboardDto[]>(`/api/leaderboard/${leagueId}/leaderboard/${season}`);
  return data;
}
