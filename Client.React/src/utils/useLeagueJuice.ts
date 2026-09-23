import { useQuery } from '@tanstack/react-query';
import { getLeagueJuice } from '../api/league';

export const leagueJuiceQueryKey = (leagueId: number | null) => ['leagueJuice', leagueId] as const;

/**
 * A league's juice mappings (every season's tease/cost/StartWeek) from the shared React Query
 * cache — the single query behind useLeagueMinSeason and useLeagueStartWeek.
 *
 * staleTime: these settings change only when a commissioner edits them, yet with React Query's
 * default of 0 every late-mounting consumer (PicksIsland mounts after PicksPage's data lands)
 * refetched them on cold load. LeaguePortalPage — the only place they're edited — writes each
 * load straight into this cache entry, so the long staleTime never hides an edit made here.
 */
export function useLeagueJuice(leagueId: number | null) {
  return useQuery({
    queryKey: leagueJuiceQueryKey(leagueId),
    queryFn: () => getLeagueJuice(leagueId!),
    enabled: leagueId != null,
    staleTime: 5 * 60_000,
  });
}
