import type { LeaderboardDto } from '../types/leaderboard';
import type { GameView, PickView } from '../services/sportAdapter';
import type { PickType } from '../types/picks';
import { pickMargin } from './gameHelpers';

/**
 * A week is final once no league member's result for it is still MissingGameResults — the same
 * signal the backend's settlement math keys off. Until then a week's scores are provisional.
 */
export function isWeekFinal(leaderboard: LeaderboardDto[], weekIndex: number): boolean {
  return !leaderboard.some(row => row.weekResults[weekIndex]?.weekResult === 'MissingGameResults');
}

export interface WeekWin {
  week: number;
  /** What the viewer collects for the week. 0 when everyone won and the pot rolls over. */
  amount: number;
}

/**
 * The viewer's most recent final week, if they won it. Only that week can celebrate: an older win
 * never comes back once a later week has been decided.
 */
export function findLatestWeekWin(leaderboard: LeaderboardDto[], userId: string): WeekWin | null {
  const mine = leaderboard.find(row => row.userId === userId);
  if (!mine) return null;
  for (let i = mine.weekResults.length - 1; i >= 0; i--) {
    const { week, weekResult, score } = mine.weekResults[i];
    if (weekResult === 'Excluded' || !isWeekFinal(leaderboard, i)) continue;
    return weekResult === 'Won' ? { week, amount: score } : null;
  }
  return null;
}

export interface CelebrationPick {
  team: string;
  pickType: PickType;
  /** How far the pick cleared its line; null when it couldn't be worked out. */
  margin: number | null;
}

/** The viewer's picks for a won week in kickoff order, each with the margin it covered by. */
export function celebrationPicks(picks: PickView[], games: GameView[]): CelebrationPick[] {
  const gameOf = (team: string) => games.find(g => g.homeTeam === team || g.awayTeam === team);
  const kickoff = (team: string) => Date.parse(gameOf(team)?.gameTime ?? '') || Number.POSITIVE_INFINITY;
  return [...picks]
    .sort((a, b) => kickoff(a.team) - kickoff(b.team) || 0) // || 0: Infinity - Infinity is NaN
    .map(p => ({ team: p.team, pickType: p.pickType, margin: pickMargin(gameOf(p.team), p.team, p.pickType) }));
}
