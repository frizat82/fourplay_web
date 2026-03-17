import { http } from '../api/http';

export async function getNextSpreadJob(): Promise<string | null> {
  const { data } = await http.get<string | null>('/api/jobmanager/get-next-spread-job');
  return data ?? null;
}
