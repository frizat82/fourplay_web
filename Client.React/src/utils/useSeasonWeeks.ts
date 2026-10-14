import { useQuery } from '@tanstack/react-query';
import type { SportAdapter } from '../services/sportAdapter';

/** React Query key for a season's control-table weeks — build it here, never by hand. */
export const seasonWeeksQueryKey = (sport: SportAdapter['sport'], season: number | undefined) =>
  [sport, 'seasonWeeks', season] as const;

/**
 * A season's weeks from the control table (frizat-d47) — the week selector's options, names and
 * postseason split. Shared by PicksPage and ScoresPage. A season's rows don't change while a page
 * is open, and the adapter memoizes them per season, so this never refetches.
 */
export function useSeasonWeeks(adapter: SportAdapter, season: number | undefined) {
  return useQuery({
    queryKey: seasonWeeksQueryKey(adapter.sport, season),
    queryFn: () => adapter.loadSeasonWeeks(season!),
    enabled: season !== undefined,
    staleTime: Infinity,
  }).data;
}
