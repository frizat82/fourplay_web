import { http } from './http';
import type { VersionResponse } from '../types/version';

export async function getVersion(): Promise<VersionResponse> {
  const { data } = await http.get<VersionResponse>('/api/version');
  return data;
}
