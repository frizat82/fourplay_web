import { http } from './http';
import type { EspnScores } from '../types/espn';
import type { LiveGame } from '../types/liveGame';

export async function getLiveGames(): Promise<LiveGame[]> {
  const { data } = await http.get<LiveGame[]>('/api/espn/livegames');
  return data ?? [];
}

export async function getScores() {
  const { data } = await http.get<EspnScores>('/api/espn/scores');
  return data;
}

export async function loadScoresWithRetry(maxRetries = 5, delayMs = 500): Promise<EspnScores | null> {
  let attempt = 0;
  let data: EspnScores | null = null;
  while ((!data?.events || data.events.length === 0) && attempt < maxRetries) {
    data = await getScores();
    if (data?.events && data.events.length > 0) break;
    await new Promise((resolve) => setTimeout(resolve, delayMs));
    attempt += 1;
  }
  return data;
}

export async function getWeekScores(week: number, year: number, postSeason = false) {
  const { data } = await http.get<EspnScores>(`/api/espn/scores/week/${week}/${year}`, {
    params: { postSeason },
  });
  return data;
}
