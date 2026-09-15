import { useQuery } from '@tanstack/react-query';
import { getNflCurrentWeek, getLeaguePicks } from '../api/league';
import { getCfbCurrentSlate, getCfbAllPicks } from '../api/cfb';
import { getNflRequiredPicks, getCfbRequiredPicks } from './gameHelpers';

export interface MissingPicksResult {
  /** Picks made this week/slate, keyed by userId. A member with no entry has made 0 picks. */
  picksByUser: Map<string, number>;
  /** Required pick count for the current week/slate, or null when there's no current week/slate
   *  to resolve (e.g. off-season) — callers should show no indicator at all in that case, not
   *  treat every member as missing. */
  requiredPicks: number | null;
  isLoading: boolean;
}

/** Counts picks per userId — pure, sport-agnostic, and independently testable. */
export function countPicksByUser(picks: { userId: string }[]): Map<string, number> {
  const counts = new Map<string, number>();
  for (const p of picks) counts.set(p.userId, (counts.get(p.userId) ?? 0) + 1);
  return counts;
}

/**
 * Per-member pick completion for the CURRENT week/slate — backs a commissioner's "who's missing
 * picks" view (frizat-6sc) on the Members tab. Not a resurrection of the removed MissingPicksJob
 * (that was an automated reminder-email job, deliberately deleted since picks are open up to
 * kickoff; this is an on-demand, read-only view a commissioner checks themselves).
 */
// enabled defaults to true — pass false (e.g. the Members tab isn't the active tab) to skip the
// fetch entirely rather than resolving current-week/slate + all-picks data nobody's looking at.
export function useMissingPicks(leagueId: number | null, isCfb: boolean, enabled = true): MissingPicksResult {
  const { data, isLoading } = useQuery({
    queryKey: ['missingPicks', leagueId, isCfb],
    queryFn: async (): Promise<{ picksByUser: Map<string, number>; requiredPicks: number | null }> => {
      if (isCfb) {
        const slate = await getCfbCurrentSlate();
        if (!slate) return { picksByUser: new Map(), requiredPicks: null };
        const picks = await getCfbAllPicks(leagueId!, slate.id);
        return { picksByUser: countPicksByUser(picks), requiredPicks: getCfbRequiredPicks(slate.slateNumber) };
      }
      const week = await getNflCurrentWeek();
      const picks = await getLeaguePicks(leagueId!, week.season, week.weekId);
      return { picksByUser: countPicksByUser(picks), requiredPicks: getNflRequiredPicks(week.weekId) };
    },
    enabled: leagueId != null && enabled,
  });

  return { picksByUser: data?.picksByUser ?? new Map(), requiredPicks: data?.requiredPicks ?? null, isLoading };
}
