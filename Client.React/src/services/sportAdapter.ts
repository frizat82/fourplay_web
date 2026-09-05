import { isGameDecided } from '../utils/gameHelpers';
import type { PickType } from '../types/picks';

export type { PickType };

/** Canonical game status — both adapters normalize to this before populating GameView */
export type GameStatusValue = 'final' | 'in_progress' | 'halftime' | 'scheduled' | null;

/**
 * Caches an async fetch's resolved value for the lifetime of the closure it's created in —
 * both nflAdapter.ts (getCurrentWeek, control table) and cfbAdapter.ts (getCurrentSlate, slate)
 * need the exact same "resolve once per adapter instance, reuse thereafter" behavior so a single
 * page load doesn't re-fetch "what's current" on every games/scores/season-year call. Caches the
 * in-flight PROMISE, not just the resolved value — two calls made before the first resolves (e.g.
 * loadCurrentGames and currentSeasonYear firing close together on the same page) share one
 * request instead of each independently kicking off their own. A rejected fetch is NOT cached —
 * the next call retries, rather than pinning a transient failure (network blip, brief DB outage)
 * for the rest of the adapter's lifetime.
 */
export function memoizeOnce<T>(fetch: () => Promise<T>): () => Promise<T> {
  let promise: Promise<T> | undefined;
  return () => {
    if (!promise) {
      promise = fetch().catch((err: unknown) => { promise = undefined; throw err; });
    }
    return promise;
  };
}

/**
 * Hide other users' picks for games that haven't started yet.
 * The caller's own picks are always visible (so they can confirm their submission).
 * Once a game kicks off, picks for that game become visible to everyone — mirroring
 * the write-side kickoff lock in AddPicks.
 */
export function revealPicksForStartedGames(allPicks: PickView[], games: GameView[], userId: string): PickView[] {
  const now = new Date();
  const startedIds = new Set(
    games
      .filter(g => {
        // ESPN confirmed the game is underway or finished
        if (isGameDecided(g.gameStatus)) return true;
        // ESPN still says scheduled but kickoff time has passed — cache is stale
        if (g.gameStatus === 'scheduled' && g.gameTime != null && new Date(g.gameTime) <= now) return true;
        return false;
      })
      .map(g => g.id)
  );
  return allPicks.filter(p => p.userId === userId || startedIds.has(p.gameId));
}

/**
 * Kickoff time ascending; among games at the same kickoff time, the best (lowest-numbered) AP
 * rank of either team first, unranked/no-rank games last. Shared by PicksPage and ScoresPage —
 * both render games through this one function, so it applies identically to both sports. CFB is
 * the only adapter that ever populates homeRank/awayRank; NFL's games always have both undefined,
 * so the rank tiebreaker is inert there and this reduces to a pure time sort.
 */
export function sortGamesByTimeThenRank(games: GameView[]): GameView[] {
  // null, not Infinity, for "no rank" — Infinity - Infinity is NaN, an unspecified
  // Array.sort comparator result (ECMA-262 leaves ordering undefined for a non-total-order
  // comparator), which two unranked games at the same kickoff time would hit constantly.
  const bestRank = (g: GameView): number | null => {
    const ranks = [g.homeRank, g.awayRank].filter((r): r is number => r != null);
    return ranks.length > 0 ? Math.min(...ranks) : null;
  };
  return [...games].sort((a, b) => {
    const timeDiff = new Date(a.gameTime).getTime() - new Date(b.gameTime).getTime();
    if (timeDiff !== 0) return timeDiff;
    const rankA = bestRank(a);
    const rankB = bestRank(b);
    if (rankA === rankB) return 0; // both unranked, or (impossible in practice) tied rank
    if (rankA === null) return 1;
    if (rankB === null) return -1;
    return rankA - rankB;
  });
}

export interface GameView {
  id: string;
  homeTeam: string;
  awayTeam: string;
  homeSpread: number | null;
  awaySpread: number | null;
  overUnder: number | null;
  homeScore: number | null;
  awayScore: number | null;
  gameStatus: GameStatusValue;
  gameTime: string;
  weather?: { displayValue: string; conditionId?: string; temperatureF?: number };
  homeRecord?: string;
  awayRecord?: string;
  // Scores page extras
  homeLogo?: string;
  awayLogo?: string;
  situation?: import('../types/liveGame').GameSituation | null;
  homeCovers?: boolean | null;  // null = not final / no odds
  awayCovers?: boolean | null;  // computed independently — NOT !homeCovers (teased spreads aren't mirror images)
  overWins?: boolean | null;
  /** When the spread was first posted (DateCreated). Undefined where the adapter's spread source
   *  doesn't carry it — NFL's spreadBatch endpoint returns computed, juice-adjusted odds rather
   *  than the raw NflSpreads entity, so it isn't available there today. */
  spreadPostedAt?: string | null;
  /** AP Top 25 rank (1-25), null/undefined when unranked. CFB-only — NFL has no polls. */
  homeRank?: number | null;
  awayRank?: number | null;
}

export interface PickView {
  gameId: string;
  team: string;
  pickType: PickType;
  userId: string;
  userName: string;
}

export interface WeekState {
  season: number;
  week: number;
  isPostSeason: boolean;
}

export interface LoadedWeek extends WeekState {
  games: GameView[];
  userPicks: PickView[];
  hasOdds: boolean;
  requiredPicks: number;
  /** The furthest week with data — used to cap the WeekYearSelector */
  maxWeek: number;
  maxSeason: number;
}

export interface LoadedScores extends WeekState {
  games: GameView[];
  allPicks: PickView[];
  userPicks: PickView[];
  hasOdds: boolean;
  hasActiveGames: boolean;
  requiredPicks: number;
  maxWeek: number;
  maxSeason: number;
}

export interface SportAdapter {
  // Picks page
  loadCurrentGames(leagueId: number, userId: string): Promise<LoadedWeek>;
  loadHistoricalGames(leagueId: number, userId: string, week: WeekState): Promise<LoadedWeek | null>;
  submitPicks(leagueId: number, state: WeekState, picks: { gameId: string; team: string; pickType: PickType }[]): Promise<void>;
  clearPicks(leagueId: number, state: WeekState): Promise<PickView[]>;
  loadJerseys?(season: number, week: number): Promise<Record<string, string>>;

  // Scores page
  loadCurrentScores(leagueId: number, userId: string): Promise<LoadedScores>;
  loadHistoricalScores(leagueId: number, userId: string, week: WeekState): Promise<LoadedScores | null>;

  // Shared config
  /** Stable sport identifier — used as the React Query cache key prefix */
  sport: 'nfl' | 'cfb';
  pollIntervalMs: number;
  /** SSE endpoint URL for live score push. Undefined on adapters that don't support it (e.g. CFB). */
  sseUrl?: string;
  // loadJerseys is optional — if defined, PicksPage shows jerseys when data is non-empty
  weekSelectorConfig: {
    regularWeekOptions?: number[];
    postSeasonWeekOptions?: number[];
    maxRegularSeasonWeek: number;
    minSeason: number;
    weekLabelFn?: (week: number, isPostSeason: boolean) => string;
  };
  currentSeasonYear(): Promise<number>;
}
