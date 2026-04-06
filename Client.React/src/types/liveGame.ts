export interface GameSituation {
  possessionTeam: string | null;
  isHomePossession: boolean;
  yardLine: number;
  down: number;
  distance: number;
  isRedZone: boolean;
  downDistanceText: string;
}

export interface LiveGame {
  homeTeam: string;
  awayTeam: string;
  homeScore: number;
  awayScore: number;
  isCompleted: boolean;
  kickoffUtc: string;
  situation: GameSituation | null;
}
