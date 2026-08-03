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

/** Retries fetchFn until it returns a non-empty EspnScores, or maxRetries is exhausted. */
async function retryUntilEvents(fetchFn: () => Promise<EspnScores | null>, maxRetries = 5, delayMs = 500): Promise<EspnScores | null> {
  let attempt = 0;
  let data: EspnScores | null = null;
  while ((!data?.events || data.events.length === 0) && attempt < maxRetries) {
    data = await fetchFn();
    if (data?.events && data.events.length > 0) break;
    await new Promise((resolve) => setTimeout(resolve, delayMs));
    attempt += 1;
  }
  return data;
}

export async function loadScoresWithRetry(maxRetries = 5, delayMs = 500): Promise<EspnScores | null> {
  return retryUntilEvents(getScores, maxRetries, delayMs);
}

/** Cached live CFB scores for the CURRENT slate — same role as getScores() for NFL. */
export async function getCfbScores(): Promise<EspnScores | null> {
  const { data } = await http.get<EspnScores>('/api/espn/cfb/scores');
  return data ?? null;
}

export async function loadCfbScoresWithRetry(maxRetries = 5, delayMs = 500): Promise<EspnScores | null> {
  return retryUntilEvents(getCfbScores, maxRetries, delayMs);
}

/** Direct/uncached live CFB scores for a SPECIFIC (typically non-current) slate — same role as getWeekScores() for NFL. */
export async function getCfbScoresForSlate(slateId: number): Promise<EspnScores | null> {
  const { data } = await http.get<EspnScores>(`/api/espn/cfb/scores/slate/${slateId}`);
  return data ?? null;
}

export async function getWeekScores(week: number, year: number, postSeason = false) {
  const { data } = await http.get<EspnScores>(`/api/espn/scores/week/${week}/${year}`, {
    params: { postSeason },
  });
  return data;
}
