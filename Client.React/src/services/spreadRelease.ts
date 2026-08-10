import { http } from '../api/http';

export async function getNextSpreadJob(sport?: 'NFL' | 'CFB'): Promise<string | null> {
  const url = sport
    ? `/api/jobmanager/get-next-spread-job?sport=${sport.toLowerCase()}`
    : '/api/jobmanager/get-next-spread-job';
  const { data } = await http.get<string | null>(url);
  return data ?? null;
}
