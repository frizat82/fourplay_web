import type { MemberPickCountDto } from '../../src/types/league';

/** Mock body for the submitted-pick-counts endpoints: picks-per-user from a spec's seeded picks. */
export function pickCountsFromPicks(picks: { userId: string }[]): MemberPickCountDto[] {
  const counts = new Map<string, number>();
  for (const p of picks) counts.set(p.userId, (counts.get(p.userId) ?? 0) + 1);
  return [...counts].map(([userId, pickCount]) => ({ userId, pickCount }));
}
