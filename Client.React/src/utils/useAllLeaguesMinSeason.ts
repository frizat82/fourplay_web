import { useQuery } from '@tanstack/react-query';
import { getAllLeagues } from '../api/league';

/**
 * The earliest season ANY league actually has data for — the cross-league counterpart of
 * useLeagueMinSeason.ts, for admin pages (League Costs) that show every league at once rather
 * than one league's own view. A league's own earliest configured season is unioned via Math.min
 * across all leagues, so a season dropdown never offers a year no league could possibly have
 * existed in. Falls back to `fallbackMinSeason` while loading or when no league has a juice
 * mapping yet.
 */
export function useAllLeaguesMinSeason(fallbackMinSeason: number): number {
  const { data } = useQuery({
    queryKey: ['allLeagues'],
    queryFn: getAllLeagues,
  });

  if (!data) return fallbackMinSeason;
  const seasons = data.map((l) => l.minSeason).filter((s): s is number => s != null);
  if (seasons.length === 0) return fallbackMinSeason;
  return Math.min(...seasons);
}
