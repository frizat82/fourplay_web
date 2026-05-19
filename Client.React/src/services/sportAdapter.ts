/** Canonical game status — both adapters normalize to this before populating GameView */
export type GameStatusValue = 'final' | 'in_progress' | 'halftime' | 'scheduled' | null;

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
  situation?: string | null;
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
}

export interface LoadedScores extends WeekState {
  games: GameView[];
  allPicks: PickView[];        // all users' picks (for the scores display)
  userPicks: PickView[];       // current user's picks (for "my picks" filter)
  hasOdds: boolean;
  hasActiveGames: boolean;     // drives poll interval
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
  supportsJerseys: boolean;
  supportsMatrix: boolean;      // NFL: true; CFB: false (for now)
  supportsPickDialog: boolean;  // NFL: true; CFB: false (no ESPN logos)
  weekSelectorConfig: {
    regularWeekOptions?: number[];
    postSeasonWeekOptions?: number[];
    maxRegularSeasonWeek: number;
    minSeason: number;
    weekLabelFn?: (week: number, isPostSeason: boolean) => string;
  };
  currentSeasonYear(): Promise<number>;
}
