import type { CfbPickDto, CfbScoreDto, CfbSlateDto, CfbSpreadDto, SpreadLockWeekDto } from '../types/league';

const BASE = '/api/cfb';

export async function getCfbCurrentSlate(): Promise<CfbSlateDto | null> {
  const res = await fetch(`${BASE}/current-slate`);
  if (!res.ok) return null;
  return res.json();
}

export async function getCfbSpreadLockSchedule(): Promise<SpreadLockWeekDto[]> {
  const res = await fetch(`${BASE}/spread-lock-schedule`);
  if (!res.ok) return [];
  return res.json();
}

export async function getCfbSlates(season: number): Promise<CfbSlateDto[]> {
  const res = await fetch(`${BASE}/slates/${season}`);
  if (!res.ok) return [];
  return res.json();
}

export async function getCfbSpreads(leagueId: number, cfbSlateId: number): Promise<CfbSpreadDto[]> {
  const res = await fetch(`${BASE}/spreads/${leagueId}/${cfbSlateId}`);
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
  picks: { team: string; pickType: string }[]
): Promise<{ added: number }> {
  const res = await fetch(`${BASE}/picks`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ leagueId, cfbSlateId, season, picks }),
  });
  // Throws (rather than the old silent `{ added: 0 }` fallback) so a rejection — cap exceeded,
  // game already kicked off — actually reaches the caller. With immediate per-click submission,
  // the caller's optimistic UI update depends on this to know when to roll back.
  if (!res.ok) throw new Error(`Failed to add pick(s) (${res.status})`);
  return res.json();
}

export async function deleteCfbPicks(leagueId: number, cfbSlateId: number): Promise<void> {
  await fetch(`${BASE}/picks/${leagueId}/${cfbSlateId}`, { method: 'DELETE' });
}

// Self-service removal of one of the caller's own picks, any time before its game kicks off —
// identified by natural key (team/pickType/slate/season/league). The server always resolves
// ownership from the JWT, never from this body. Unlike this file's other functions, a failed
// request THROWS rather than silently returning a fallback value — the caller relies on this to
// roll back its optimistic UI update.
export async function removeMyCfbPick(pick: { leagueId: number; cfbSlateId: number; season: number; team: string; pickType: string }): Promise<void> {
  const res = await fetch(`${BASE}/picks/mine`, {
    method: 'DELETE',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(pick),
  });
  if (!res.ok) throw new Error(`Failed to remove pick (${res.status})`);
}
