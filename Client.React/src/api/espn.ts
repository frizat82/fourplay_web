import { http } from './http';
import type { EspnScores } from '../types/espn';
import type { LiveGame } from '../types/liveGame';

export async function getLiveGames(): Promise<LiveGame[]> {
  const { data } = await http.get<LiveGame[]>('/api/espn/livegames');
  return data ?? [];
}

export async function getCfbLiveGames(): Promise<LiveGame[]> {
  const { data } = await http.get<LiveGame[]>('/api/espn/cfb/livegames');
  return data ?? [];
}

export async function getScores() {
  const { data } = await http.get<EspnScores>('/api/espn/scores');
  return data;
}

/** Live CFB scores for a specific slate, keyed by the control-table-resolved slate id — same role
 * as getWeekScores() for NFL. Used for both current and historical slates; there is no separate
 * "implicit current" CFB scoreboard call (see cfbAdapter.ts's fetchCfbEspnData). */
export async function getCfbScoresForSlate(slateId: number): Promise<EspnScores | null> {
  const { data } = await http.get<EspnScores>(`/api/espn/cfb/scores/slate/${slateId}`);
  return data ?? null;
}

/** Live/historical NFL scores for a specific (season, nflWeek) — our own control-table WeekId,
 * never ESPN's own week numbering — same role as getCfbScoresForSlate() for CFB (frizat-3nv). */
export async function getWeekScores(season: number, nflWeek: number): Promise<EspnScores | null> {
  const { data } = await http.get<EspnScores>(`/api/espn/scores/nfl-week/${season}/${nflWeek}`);
  return data ?? null;
}
