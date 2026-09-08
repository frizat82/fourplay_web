/** Descending list of season years from maxSeason down to minSeason, inclusive — the "most
 * recent first" order every season dropdown in this app (WeekYearSelector, LeaderboardPage)
 * presents to the user. minSeason always wins if it's greater than maxSeason (e.g. a caller's
 * hardcoded "latest known" upper bound is stale relative to real, data-driven season history) —
 * every caller needs at least one option, never a silently empty range. */
export function buildDescendingSeasonRange(minSeason: number, maxSeason: number): number[] {
  const max = Math.max(minSeason, maxSeason);
  return Array.from({ length: max - minSeason + 1 }, (_, i) => max - i);
}
