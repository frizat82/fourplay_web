/** Canonical game status — both adapters normalize to this before populating GameView */
export type GameStatusValue = 'final' | 'in_progress' | 'halftime' | 'scheduled' | null;

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
        if (g.gameStatus !== 'scheduled' && g.gameStatus !== null) return true;
        // ESPN still says scheduled but kickoff time has passed — cache is stale
        if (g.gameStatus === 'scheduled' && g.gameTime != null && new Date(g.gameTime) <= now) return true;
        return false;
      })
      .map(g => g.id)
  );
  return allPicks.filter(p => p.userId === userId || startedIds.has(p.gameId));
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
  overWins?: boolean | null;
}

export interface PickView {
  gameId: string;
  team: string;
  pickType: 'Spread' | 'Over' | 'Under';
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
  submitPicks(leagueId: number, state: WeekState, picks: { gameId: string; team: string; pickType: string }[]): Promise<void>;
  clearPicks(leagueId: number, state: WeekState): Promise<PickView[]>;
  loadJerseys?(season: number, week: number): Promise<Record<string, string>>;

  // Scores page
  loadCurrentScores(leagueId: number, userId: string): Promise<LoadedScores>;
  loadHistoricalScores(leagueId: number, userId: string, week: WeekState): Promise<LoadedScores | null>;

  // Shared config
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
