import { useLeagueJuice } from './useLeagueJuice';

/**
 * The currently-selected league's configured StartWeek for `season` (frizat-o3x) — the week
 * scoring actually begins at. Defaults to 1 (no exclusion) while loading or when the league has
 * no juice mapping for that season yet, matching the backend's fail-open behavior in
 * LeagueController.AddPicks / CfbPicksController.AddPicks. Shared by PicksPage and ScoresPage
 * (frizat-u66) rather than each page independently fetching the same rarely-changing setting.
 *
 * Reuses useLeagueMinSeason's exact query (useLeagueJuice) — that response already carries every
 * season's `startWeek`, so a second per-season endpoint call (`getLeagueJuiceForSeason`) would just be a redundant round trip for data already in flight on
 * the same page render (/simplify efficiency finding, frizat-u66).
 */
export function useLeagueStartWeek(leagueId: number | null, season: number): number {
  const { data } = useLeagueJuice(leagueId);

  return data?.find(m => m.season === season)?.startWeek ?? 1;
}
