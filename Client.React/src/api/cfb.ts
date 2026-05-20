import type { CfbPickDto, CfbScoreDto, CfbSlateDto, CfbSpreadDto } from '../types/league';

const BASE = '/api/cfb';

export async function getCfbSlates(season: number): Promise<CfbSlateDto[]> {
  const res = await fetch(`${BASE}/slates/${season}`);
  if (!res.ok) return [];
  return res.json();
}

export async function getCfbSpreads(cfbSlateId: number): Promise<CfbSpreadDto[]> {
  const res = await fetch(`${BASE}/spreads/${cfbSlateId}`);
  if (!res.ok) return [];
  return res.json();
}

export async function getCfbScores(cfbSlateId: number): Promise<CfbScoreDto[]> {
  const res = await fetch(`${BASE}/scores/${cfbSlateId}`);
  if (!res.ok) return [];
  return res.json();
}

export async function getCfbUserPicks(leagueId: number, cfbSlateId: number): Promise<CfbPickDto[]> {
  const res = await fetch(`${BASE}/picks/${leagueId}/${cfbSlateId}/user`);
  if (!res.ok) return [];
  return res.json();
}

export async function getCfbAllPicks(leagueId: number, cfbSlateId: number): Promise<CfbPickDto[]> {
  const res = await fetch(`${BASE}/picks/${leagueId}/${cfbSlateId}`);
  if (!res.ok) return [];
  return res.json();
}

export async function addCfbPicks(
  leagueId: number,
  cfbSlateId: number,
  season: number,
  picks: { espnEventId: number; team: string; pickType: string }[]
): Promise<{ added: number }> {
  const res = await fetch(`${BASE}/picks`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ leagueId, cfbSlateId, season, picks }),
  });
  if (!res.ok) return { added: 0 };
  return res.json();
}

export async function deleteCfbPicks(leagueId: number, cfbSlateId: number): Promise<void> {
  await fetch(`${BASE}/picks/${leagueId}/${cfbSlateId}`, { method: 'DELETE' });
}
