import { describe, it, expect } from 'vitest';
import { revealPicksForStartedGames } from '../services/sportAdapter';
import type { GameView, PickView } from '../services/sportAdapter';

function makeGame(id: string, status: GameView['gameStatus']): GameView {
  return {
    id,
    homeTeam: 'HME',
    awayTeam: 'AWY',
    homeSpread: null,
    awaySpread: null,
    overUnder: null,
    homeScore: null,
    awayScore: null,
    gameStatus: status,
    gameTime: new Date().toISOString(),
  };
}

function makePick(gameId: string, userId: string): PickView {
  return { gameId, team: 'HME', pickType: 'Spread', userId, userName: userId };
}

const ME = 'user-me';
const OTHER = 'user-other';

describe('revealPicksForStartedGames', () => {
  it('hides other users picks for scheduled games', () => {
    const games = [makeGame('g1', 'scheduled')];
    const picks = [makePick('g1', ME), makePick('g1', OTHER)];

    const result = revealPicksForStartedGames(picks, games, ME);

    expect(result).toHaveLength(1);
    expect(result[0].userId).toBe(ME);
  });

  it('reveals other users picks once game is in_progress', () => {
    const games = [makeGame('g1', 'in_progress')];
    const picks = [makePick('g1', ME), makePick('g1', OTHER)];

    const result = revealPicksForStartedGames(picks, games, ME);

    expect(result).toHaveLength(2);
  });

  it('reveals other users picks for halftime games', () => {
    const games = [makeGame('g1', 'halftime')];
    const picks = [makePick('g1', ME), makePick('g1', OTHER)];

    const result = revealPicksForStartedGames(picks, games, ME);

    expect(result).toHaveLength(2);
  });

  it('reveals other users picks for final games', () => {
    const games = [makeGame('g1', 'final')];
    const picks = [makePick('g1', ME), makePick('g1', OTHER)];

    const result = revealPicksForStartedGames(picks, games, ME);

    expect(result).toHaveLength(2);
  });

  it('always shows the callers own picks regardless of game status', () => {
    const games = [makeGame('g1', 'scheduled')];
    const picks = [makePick('g1', ME)];

    const result = revealPicksForStartedGames(picks, games, ME);

    expect(result).toHaveLength(1);
    expect(result[0].userId).toBe(ME);
  });

  it('handles mixed week — hides other picks for scheduled, reveals for started', () => {
    const games = [makeGame('g1', 'scheduled'), makeGame('g2', 'in_progress')];
    const picks = [
      makePick('g1', OTHER), // scheduled — hidden
      makePick('g2', OTHER), // in_progress — visible
      makePick('g1', ME),    // own pick — always visible
    ];

    const result = revealPicksForStartedGames(picks, games, ME);

    expect(result).toHaveLength(2);
    expect(result.some(p => p.userId === OTHER && p.gameId === 'g2')).toBe(true);
    expect(result.some(p => p.userId === OTHER && p.gameId === 'g1')).toBe(false);
  });

  it('treats null gameStatus as not started — hides other picks', () => {
    const games = [makeGame('g1', null)];
    const picks = [makePick('g1', ME), makePick('g1', OTHER)];

    const result = revealPicksForStartedGames(picks, games, ME);

    expect(result).toHaveLength(1);
    expect(result[0].userId).toBe(ME);
  });

  it('returns empty array when no picks exist', () => {
    const games = [makeGame('g1', 'in_progress')];
    const result = revealPicksForStartedGames([], games, ME);
    expect(result).toHaveLength(0);
  });
});
