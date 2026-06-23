export interface GameSituation {
  possessionTeam: string | null;
  isHomePossession: boolean;
  yardLine: number;
  down: number;
  distance: number;
  isRedZone: boolean;
  downDistanceText: string;
  /** Current quarter/period (1-4, OT=5) */
  period?: number;
  /** Game clock string e.g. "8:42" */
  displayClock?: string;
}

export interface LiveGame {
  homeTeam: string;
  awayTeam: string;
  homeScore: number;
  awayScore: number;
  isCompleted: boolean;
  kickoffUtc: string;
  situation: GameSituation | null;
  period?: number;
  displayClock?: string;
}
