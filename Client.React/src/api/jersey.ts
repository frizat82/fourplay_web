import { http } from './http';

export async function getAllJerseys(season: number, week: number) {
  const { data } = await http.get<Record<string, string>>(`/api/jerseys/${season}/${week}`);
  return data;
}

export async function getJerseyByTeam(season: number, week: number, teamAbbr: string) {
  const { data } = await http.get<string>(`/api/jerseys/${season}/${week}/${encodeURIComponent(teamAbbr)}`);
  return data;
}
