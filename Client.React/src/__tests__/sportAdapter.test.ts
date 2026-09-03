import { describe, it, expect } from 'vitest';
import { revealPicksForStartedGames, sortGamesByTimeThenRank } from '../services/sportAdapter';
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
    gameTime: new Date(Date.now() + 3_600_000).toISOString(), // 1 hour in the future by default
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

  it('reveals picks when status is scheduled but game time has already passed', () => {
    const pastGame = { ...makeGame('g1', 'scheduled'), gameTime: new Date(Date.now() - 60_000).toISOString() };
    const picks = [makePick('g1', ME), makePick('g1', OTHER)];
    const result = revealPicksForStartedGames(picks, [pastGame], ME);
    expect(result).toHaveLength(2);
  });

  it('hides picks when status is scheduled and game time is still in the future', () => {
    const futureGame = { ...makeGame('g1', 'scheduled'), gameTime: new Date(Date.now() + 60_000).toISOString() };
    const picks = [makePick('g1', ME), makePick('g1', OTHER)];
    const result = revealPicksForStartedGames(picks, [futureGame], ME);
    expect(result).toHaveLength(1);
    expect(result[0].userId).toBe(ME);
  });
});

// Shared by Picks and Scores pages (both render the exact same GameView[] via this one
// function) — CFB populates homeRank/awayRank, NFL never does, so the rank tiebreaker is
// naturally a no-op there and games just stay in kickoff-time order.
describe('sortGamesByTimeThenRank', () => {
  function makeGameAt(id: string, isoTime: string, homeRank: number | null = null, awayRank: number | null = null): GameView {
    return { ...makeGame(id, 'scheduled'), gameTime: isoTime, homeRank, awayRank };
  }

  it('sorts by kickoff time ascending', () => {
    const early = makeGameAt('early', '2026-09-05T17:00:00Z');
    const late = makeGameAt('late', '2026-09-05T20:00:00Z');

    const result = sortGamesByTimeThenRank([late, early]);

    expect(result.map(g => g.id)).toEqual(['early', 'late']);
  });

  it('breaks a same-kickoff-time tie by best (lowest-numbered) rank', () => {
    const sameTime = '2026-09-05T20:00:00Z';
    const unranked = makeGameAt('unranked', sameTime);
    const ranked17 = makeGameAt('ranked17', sameTime, 17, null);
    const ranked3 = makeGameAt('ranked3', sameTime, null, 3);

    const result = sortGamesByTimeThenRank([unranked, ranked17, ranked3]);

    expect(result.map(g => g.id)).toEqual(['ranked3', 'ranked17', 'unranked']);
  });

  it('uses the better of the two teams ranks in a matchup', () => {
    const sameTime = '2026-09-05T20:00:00Z';
    const bothRanked = makeGameAt('bothRanked', sameTime, 10, 2); // best = 2
    const oneRanked = makeGameAt('oneRanked', sameTime, 5, null); // best = 5

    const result = sortGamesByTimeThenRank([oneRanked, bothRanked]);

    expect(result.map(g => g.id)).toEqual(['bothRanked', 'oneRanked']);
  });

  it('never mutates the input array', () => {
    const games = [makeGameAt('b', '2026-09-05T20:00:00Z'), makeGameAt('a', '2026-09-05T17:00:00Z')];
    const original = [...games];

    sortGamesByTimeThenRank(games);

    expect(games).toEqual(original);
  });

  it('is a no-op ordering-wise for NFL games (rank always undefined) — pure time sort', () => {
    const early = makeGame('early', 'scheduled');
    const late = { ...makeGame('late', 'scheduled'), gameTime: new Date(Date.now() + 7_200_000).toISOString() };

    const result = sortGamesByTimeThenRank([late, early]);

    expect(result.map(g => g.id)).toEqual(['early', 'late']);
  });
});
