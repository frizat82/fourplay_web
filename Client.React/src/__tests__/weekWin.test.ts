import { describe, it, expect, beforeEach, vi } from 'vitest';
import { celebrationPicks, findLatestWeekWin, isWeekFinal } from '../utils/weekWin';
import { hasSeenWeekWin, markWeekWinSeen } from '../utils/weekWinSeen';
import type { LeaderboardDto, WeekResult } from '../types/leaderboard';
import { createGameView, createLeaderboardEntry, createLeaderboardWeekResult, createPickView } from '../test/fixtures';

/** A leaderboard row from one [result, score] pair per week, starting at week 1. */
function row(userId: string, ...weeks: [WeekResult, number?][]): LeaderboardDto {
  return createLeaderboardEntry({
    userId,
    weekResults: weeks.map(([weekResult, score = 0], i) => createLeaderboardWeekResult({ week: i + 1, weekResult, score })),
  });
}

describe('isWeekFinal', () => {
  it('is final only once no member is still waiting on a game result', () => {
    const board = [row('me', ['Won', 10], ['Won', 10]), row('bob', ['Lost', -10], ['MissingGameResults'])];
    expect(isWeekFinal(board, 0)).toBe(true);
    expect(isWeekFinal(board, 1)).toBe(false);
  });
});

describe('findLatestWeekWin', () => {
  // A member who joined late can have fewer weekResults than the viewer; that must not hide the
  // viewer's win or break the finality check.
  it('copes with members whose weekResults are shorter or longer than the viewer\'s', () => {
    expect(findLatestWeekWin([row('me', ['Lost', -10], ['Won', 20]), row('bob', ['Won', 10])], 'me')).toEqual({ week: 2, amount: 20 });
    expect(findLatestWeekWin([row('me', ['Won', 10]), row('bob', ['Lost', -10], ['MissingGameResults'])], 'me')).toEqual({ week: 1, amount: 10 });
  });

  it("returns the viewer's most recent final week when they won it, with what they collect", () => {
    const board = [row('me', ['Lost', -10], ['Won', 20]), row('bob', ['Won', 10], ['Lost', -20])];
    expect(findLatestWeekWin(board, 'me')).toEqual({ week: 2, amount: 20 });
  });

  // An old win must not replay: only the latest decided week can celebrate.
  it('is null when the most recent final week was a loss, even if an earlier week was won', () => {
    const board = [row('me', ['Won', 10], ['Lost', -10]), row('bob', ['Lost', -10], ['Won', 10])];
    expect(findLatestWeekWin(board, 'me')).toBeNull();
  });

  it('counts a missed pick as a loss', () => {
    const board = [row('me', ['Won', 10], ['MissingPicks', -10]), row('bob', ['Lost', -10], ['Won', 10])];
    expect(findLatestWeekWin(board, 'me')).toBeNull();
  });

  // The viewer may have covered all their picks while another member's Monday-night game is
  // still to play. The amount isn't settled yet, so that week can't celebrate.
  it("skips a week another member hasn't finished, even though the viewer already won it", () => {
    const board = [row('me', ['Lost', -10], ['Won', 10]), row('bob', ['Won', 10], ['MissingGameResults'])];
    expect(findLatestWeekWin(board, 'me')).toBeNull();
  });

  it('looks past weeks that have not been played to the last final one', () => {
    const board = [
      row('me', ['Won', 30], ['MissingGameResults'], ['MissingGameResults']),
      row('bob', ['Lost', -30], ['MissingGameResults'], ['MissingGameResults']),
    ];
    expect(findLatestWeekWin(board, 'me')).toEqual({ week: 1, amount: 30 });
  });

  it("skips weeks before the league's start week", () => {
    const board = [row('me', ['Excluded'], ['Excluded']), row('bob', ['Excluded'], ['Excluded'])];
    expect(findLatestWeekWin(board, 'me')).toBeNull();
  });

  it('reports a week everyone won as a win worth nothing (the pot rolls over)', () => {
    const board = [row('me', ['Won', 0]), row('bob', ['Won', 0])];
    expect(findLatestWeekWin(board, 'me')).toEqual({ week: 1, amount: 0 });
  });

  it('is null when the viewer is not on the leaderboard, or it has no weeks', () => {
    expect(findLatestWeekWin([row('bob', ['Won', 10])], 'me')).toBeNull();
    expect(findLatestWeekWin([row('me')], 'me')).toBeNull();
    expect(findLatestWeekWin([], 'me')).toBeNull();
  });
});

describe('celebrationPicks', () => {
  const early = createGameView({ homeTeam: 'DAL', awayTeam: 'NYG', homeSpread: -6.5, awaySpread: 6.5, gameStatus: 'final', homeScore: 34, awayScore: 17, gameTime: '2026-11-01T18:00:00Z' });
  const late = createGameView({ homeTeam: 'KC', awayTeam: 'LV', homeSpread: -3, awaySpread: 3, gameStatus: 'final', homeScore: 30, awayScore: 20, gameTime: '2026-11-02T01:20:00Z', total: 45 });

  it('gives each pick the margin it covered by, in kickoff order', () => {
    const picks = [createPickView('me', 'KC', 'Over'), createPickView('me', 'LV'), createPickView('me', 'DAL')];
    expect(celebrationPicks(picks, [late, early])).toEqual([
      { team: 'DAL', pickType: 'Spread', margin: 10.5 },
      { team: 'KC', pickType: 'Over', margin: 5 },
      { team: 'LV', pickType: 'Spread', margin: -7 },
    ]);
  });

  it('keeps a pick whose game is missing, last and without a margin', () => {
    const picks = [createPickView('me', 'XX'), createPickView('me', 'DAL')];
    expect(celebrationPicks(picks, [early])).toEqual([
      { team: 'DAL', pickType: 'Spread', margin: 10.5 },
      { team: 'XX', pickType: 'Spread', margin: null },
    ]);
  });
});

describe('week win seen marker', () => {
  beforeEach(() => localStorage.clear());

  it('has not seen a win until it is marked', () => {
    expect(hasSeenWeekWin('me', 7, 2026, 9)).toBe(false);
    markWeekWinSeen('me', 7, 2026, 9);
    expect(hasSeenWeekWin('me', 7, 2026, 9)).toBe(true);
  });

  it('treats earlier weeks and seasons as seen too, so a corrected score cannot replay an old win', () => {
    markWeekWinSeen('me', 7, 2026, 9);
    expect(hasSeenWeekWin('me', 7, 2026, 8)).toBe(true);
    expect(hasSeenWeekWin('me', 7, 2025, 18)).toBe(true);
    expect(hasSeenWeekWin('me', 7, 2026, 10)).toBe(false);
    expect(hasSeenWeekWin('me', 7, 2027, 1)).toBe(false);
  });

  it('keeps each league and each user separate', () => {
    markWeekWinSeen('me', 7, 2026, 9);
    expect(hasSeenWeekWin('me', 8, 2026, 9)).toBe(false);
    expect(hasSeenWeekWin('bob', 7, 2026, 9)).toBe(false);
  });

  // If the marker can't be read we can't tell a new win from an old one. Replaying on every
  // visit is worse than never playing.
  it('reports seen when storage is unavailable or holds something unreadable', () => {
    localStorage.setItem('weekWinSeen:me:7', 'garbage');
    expect(hasSeenWeekWin('me', 7, 2026, 9)).toBe(true);
    const blocked = () => { throw new Error('blocked'); };
    const get = vi.spyOn(localStorage, 'getItem').mockImplementation(blocked);
    const set = vi.spyOn(localStorage, 'setItem').mockImplementation(blocked);
    try {
      expect(hasSeenWeekWin('me', 8, 2026, 9)).toBe(true);
      expect(() => markWeekWinSeen('me', 8, 2026, 9)).not.toThrow();
    } finally {
      get.mockRestore();
      set.mockRestore();
    }
  });
});
