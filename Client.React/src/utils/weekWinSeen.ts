// Remembers, per user and league on this device, the latest week whose win celebration was
// dismissed, so it plays once.
const storageKey = (userId: string, leagueId: number) => `weekWinSeen:${userId}:${leagueId}`;

/**
 * True once this week (or a later one) has been marked. Also true when the marker can't be read:
 * without it a new win can't be told from an old one, and replaying on every visit is worse than
 * never playing.
 */
export function hasSeenWeekWin(userId: string, leagueId: number, season: number, week: number): boolean {
  try {
    const stored = localStorage.getItem(storageKey(userId, leagueId));
    if (stored === null) return false;
    const [seenSeason, seenWeek] = stored.split(':').map(Number);
    if (!Number.isInteger(seenSeason) || !Number.isInteger(seenWeek)) return true;
    return season < seenSeason || (season === seenSeason && week <= seenWeek);
  } catch {
    return true;
  }
}

export function markWeekWinSeen(userId: string, leagueId: number, season: number, week: number): void {
  try {
    localStorage.setItem(storageKey(userId, leagueId), `${season}:${week}`);
  } catch {
    // Storage is blocked: hasSeenWeekWin already reports seen, so nothing replays.
  }
}
