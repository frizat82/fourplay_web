export type WeekResult = 'Won' | 'Lost' | 'MissingPicks' | 'MissingGameResults' | 'Excluded';

export interface LeaderboardWeekResults {
  week: number;
  weekResult: WeekResult;
  score: number;
  losingTeams: string[];
  hadScoringError: boolean;
}

export interface LeaderboardDto {
  userId: string;
  userName: string;
  rank: string;
  total: number;
  weekResults: LeaderboardWeekResults[];
}
