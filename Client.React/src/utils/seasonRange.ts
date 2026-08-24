/** Descending list of season years from maxSeason down to minSeason, inclusive — the "most
 * recent first" order every season dropdown in this app (WeekYearSelector, LeaderboardPage)
 * presents to the user. */
export function buildDescendingSeasonRange(minSeason: number, maxSeason: number): number[] {
  return Array.from({ length: maxSeason - minSeason + 1 }, (_, i) => maxSeason - i);
}
