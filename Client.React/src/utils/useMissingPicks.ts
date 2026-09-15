import { useQuery } from '@tanstack/react-query';
import type { SportAdapter, MissingPicksResult as AdapterMissingPicksResult } from '../services/sportAdapter';

// Extends (not re-declares) the adapter's own MissingPicksResult so isLoading/isError/refetch
// stay the only fields defined here — picksByUser/requiredPicks stay structurally locked to
// sportAdapter.ts's contract instead of a hand-kept-in-sync copy.
export interface MissingPicksResult extends AdapterMissingPicksResult {
  isLoading: boolean;
  // frizat-05h: a genuine fetch failure (e.g. NFL's control table being down) must be
  // distinguishable from CFB's legitimate null-current-slate off-season state — both used to
  // collapse into the same requiredPicks:null fallback, rendering identically as a neutral
  // "No Active Week" chip with zero indication anything had actually failed.
  isError: boolean;
  refetch: () => void;
}

export { countPicksByUser } from '../services/sportAdapter';

/**
 * Per-member pick completion for the CURRENT week/slate — backs a commissioner's "who's missing
 * picks" view (frizat-6sc) on the Members tab. Delegates the sport-specific "current week/slate ->
 * all picks -> required-pick count" resolution to the adapter (frizat-8ni) so it reuses the same
 * memoized current-week/slate resolver PicksPage/ScoresPage already rely on, rather than a second,
 * competing sport-conditional branch. Not a resurrection of the removed MissingPicksJob (that was
 * an automated reminder-email job, deliberately deleted since picks are open up to kickoff; this is
 * an on-demand, read-only view a commissioner checks themselves).
 */
// enabled defaults to true — pass false (e.g. the Members tab isn't the active tab) to skip the
// fetch entirely rather than resolving current-week/slate + all-picks data nobody's looking at.
export function useMissingPicks(adapter: SportAdapter, leagueId: number | null, enabled = true): MissingPicksResult {
  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: [adapter.sport, 'missingPicks', leagueId],
    queryFn: () => adapter.getMissingPicks(leagueId!),
    enabled: leagueId != null && enabled,
  });

  return {
    picksByUser: data?.picksByUser ?? new Map(),
    requiredPicks: data?.requiredPicks ?? null,
    isLoading,
    isError,
    refetch: () => void refetch(),
  };
}
