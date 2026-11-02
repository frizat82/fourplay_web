import { describe, it, expect } from 'vitest';
import { buildSurvivorRows, formatMargin, type MatrixSlot } from '../utils/survivorMatrix';
import { pickCovers } from '../utils/gameHelpers';
import type { GameStatusValue, GameView } from '../services/sportAdapter';
import { createGameView, createPickView as pick } from '../test/fixtures';

const FUTURE = '2099-01-01T00:00:00Z';
const PAST = '2020-01-01T00:00:00Z';

function game(homeTeam: string, awayTeam: string, homeSpread: number, awaySpread: number,
  gameStatus: GameStatusValue, homeScore: number | null, awayScore: number | null, gameTime = PAST): GameView {
  return createGameView({ homeTeam, awayTeam, homeSpread, awaySpread, gameStatus, homeScore, awayScore, gameTime });
}

const slotOf = (s: MatrixSlot) => (s.kind === 'pick' ? `${s.team}:${s.state}` : s.kind);

describe('pickCovers', () => {
  const g = game('KC', 'LV', -6.5, 6.5, 'final', 20, 10);

  it('reads each side of a spread from its own cover field, never by negation', () => {
    expect(pickCovers(g, 'KC', 'Spread')).toBe(true);
    expect(pickCovers(g, 'LV', 'Spread')).toBe(false);
  });

  it('reads Over and Under from their own fields', () => {
    expect(pickCovers(g, 'KC', 'Over')).toBe(false);
    expect(pickCovers(g, 'KC', 'Under')).toBe(true);
  });

  it('is null for a game that has not started, or a game that was not found', () => {
    expect(pickCovers(game('KC', 'LV', -6.5, 6.5, 'scheduled', null, null, FUTURE), 'KC', 'Spread')).toBeNull();
    expect(pickCovers(undefined, 'KC', 'Spread')).toBeNull();
  });
});

describe('buildSurvivorRows — pick state', () => {
  it('a final game is won or lost', () => {
    const games = [game('BUF', 'MIA', -3, 3, 'final', 27, 20)];
    const [row] = buildSurvivorRows([pick('alice', 'BUF')], games, 1);
    expect(row.slots[0]).toMatchObject({ kind: 'pick', state: 'won' });
    const [row2] = buildSurvivorRows([pick('bob', 'MIA')], games, 1);
    expect(row2.slots[0]).toMatchObject({ kind: 'pick', state: 'lost' });
  });

  // Before this view, a live game's "covering right now" value was painted solid green/red in the
  // matrix — indistinguishable from a final result.
  it('a live game is covering or trailing, never won or lost', () => {
    const games = [game('KC', 'LV', -6.5, 6.5, 'in_progress', 20, 10)];
    expect(buildSurvivorRows([pick('alice', 'KC')], games, 1)[0].slots[0]).toMatchObject({ state: 'covering', margin: 3.5 });
    expect(buildSurvivorRows([pick('bob', 'LV')], games, 1)[0].slots[0]).toMatchObject({ state: 'trailing', margin: -3.5 });
  });

  it('halftime counts as live', () => {
    const games = [game('KC', 'LV', -6.5, 6.5, 'halftime', 20, 10)];
    expect(buildSurvivorRows([pick('alice', 'KC')], games, 1)[0].slots[0]).toMatchObject({ state: 'covering' });
  });

  it('Over/Under margins are measured against their own thresholds', () => {
    const games = [game('KC', 'LV', -6.5, 6.5, 'in_progress', 20, 10)]; // total 30 vs 45
    expect(buildSurvivorRows([pick('alice', 'KC', 'Over')], games, 1)[0].slots[0]).toMatchObject({ state: 'trailing', margin: -15 });
    expect(buildSurvivorRows([pick('alice', 'KC', 'Under')], games, 1)[0].slots[0]).toMatchObject({ state: 'covering', margin: 15 });
  });

  it('a revealed pick in a game that has not started is pending, with no margin', () => {
    const games = [game('KC', 'LV', -6.5, 6.5, 'scheduled', null, null, FUTURE)];
    expect(buildSurvivorRows([pick('alice', 'KC')], games, 1)[0].slots[0]).toMatchObject({ state: 'pending', margin: null });
  });

  it('orders each row by kickoff, so the same game lines up in the same column', () => {
    const games = [
      game('KC', 'LV', -6.5, 6.5, 'in_progress', 20, 10, '2026-11-02T01:20:00Z'),
      game('BUF', 'MIA', -3, 3, 'final', 27, 20, '2026-11-01T18:00:00Z'),
      game('SF', 'DET', -3, 3, 'final', 30, 20, '2026-11-01T21:25:00Z'),
    ];
    const [row] = buildSurvivorRows([pick('alice', 'KC'), pick('alice', 'SF'), pick('alice', 'BUF')], games, 3);
    expect(row.slots.map(slotOf)).toEqual(['BUF:won', 'SF:won', 'KC:covering']);
  });

  it('never renders more slots than requiredPicks', () => {
    const games = [game('BUF', 'MIA', -3, 3, 'final', 27, 20), game('SF', 'DET', -3, 3, 'final', 30, 20)];
    const [row] = buildSurvivorRows([pick('alice', 'BUF'), pick('alice', 'SF')], games, 1);
    expect(row.slots).toHaveLength(1);
  });
});

describe('buildSurvivorRows — unrevealed slots', () => {
  const started = game('BUF', 'MIA', -3, 3, 'final', 27, 20);
  const notStarted = game('DAL', 'NYG', -3, 3, 'scheduled', null, null, FUTURE);

  it("shows another member's unrevealed slot as hidden while any game has not kicked off", () => {
    const [row] = buildSurvivorRows([pick('eve', 'BUF')], [started, notStarted], 2, 'id-alice');
    expect(row.slots.map(slotOf)).toEqual(['BUF:won', 'hidden']);
    expect(row.status).toBe('alive');
  });

  it("shows the viewer's own unfilled slot as empty, not hidden — their own picks are never hidden from them", () => {
    const [row] = buildSurvivorRows([pick('alice', 'BUF')], [started, notStarted], 2, 'id-alice');
    expect(row.slots.map(slotOf)).toEqual(['BUF:won', 'empty']);
    expect(row.status).toBe('alive');
  });

  it('once every game has kicked off, an unfilled slot is a missed pick — empty, and the member is out', () => {
    const [row] = buildSurvivorRows([pick('eve', 'BUF')], [started], 2, 'id-alice');
    expect(row.slots.map(slotOf)).toEqual(['BUF:won', 'empty']);
    expect(row.status).toBe('out');
  });

  // Picks were filtered at fetch time; if the last game kicked off since, the member's pick is
  // simply not fetched yet. Judge by the game status from that same fetch, never the clock now —
  // otherwise a member with a live, still-hidden Monday-night pick is shown as out.
  it('a game ESPN still lists as scheduled keeps the slot hidden, even past its kickoff time', () => {
    const stale = game('DAL', 'NYG', -3, 3, 'scheduled', null, null, PAST);
    const [row] = buildSurvivorRows([pick('eve', 'BUF')], [started, stale], 2, 'id-alice');
    expect(row.slots.map(slotOf)).toEqual(['BUF:won', 'hidden']);
    expect(row.status).toBe('alive');
  });

  it('treats a live game as kicked off', () => {
    const live = game('DAL', 'NYG', -3, 3, 'in_progress', 7, 7);
    const [row] = buildSurvivorRows([pick('eve', 'BUF')], [started, live], 2, 'id-alice');
    expect(row.slots.map(slotOf)).toEqual(['BUF:won', 'empty']);
    expect(row.status).toBe('out');
  });
});

describe('buildSurvivorRows — row identity and ordering edge cases', () => {
  const buf = game('BUF', 'MIA', -3, 3, 'final', 27, 20);

  it('keeps two members who share a display name in separate rows', () => {
    const rows = buildSurvivorRows([pick('sam', 'BUF', 'Spread', 'id-1'), pick('sam', 'MIA', 'Spread', 'id-2')], [buf], 1);
    expect(rows).toHaveLength(2);
    expect(rows.map(r => r.status).sort()).toEqual(['out', 'won']);
  });

  it('keeps picks whose game is missing in their original order, not an engine-dependent one', () => {
    const [row] = buildSurvivorRows([pick('amy', 'XX'), pick('amy', 'YY'), pick('amy', 'BUF')], [buf], 3);
    expect(row.slots.map(slotOf)).toEqual(['BUF:won', 'XX:pending', 'YY:pending']);
  });
});

describe('buildSurvivorRows — survivor status and row order', () => {
  const games = [
    game('BUF', 'MIA', -3, 3, 'final', 27, 20),
    game('KC', 'LV', -6.5, 6.5, 'in_progress', 20, 10),
  ];

  it('any lost pick puts a member out, even if their live pick is covering', () => {
    const [row] = buildSurvivorRows([pick('carlos', 'MIA'), pick('carlos', 'KC')], games, 2);
    expect(row.status).toBe('out');
  });

  it('a member with no losses but a live pick not covering is on the ropes', () => {
    const [row] = buildSurvivorRows([pick('bob', 'BUF'), pick('bob', 'LV')], games, 2);
    expect(row.status).toBe('onTheRopes');
  });

  it('a member with no losses and nothing trailing is alive', () => {
    const [row] = buildSurvivorRows([pick('alice', 'BUF'), pick('alice', 'KC')], games, 2);
    expect(row.status).toBe('alive');
  });

  it('lists everyone still in it first, then everyone out, each alphabetically', () => {
    const rows = buildSurvivorRows([
      pick('dana', 'MIA'), pick('dana', 'KC'),
      pick('bob', 'BUF'), pick('bob', 'LV'),
      pick('carlos', 'MIA'), pick('carlos', 'LV'),
      pick('alice', 'BUF'), pick('alice', 'KC'),
    ], games, 2);
    expect(rows.map(r => r.userName)).toEqual(['alice', 'bob', 'carlos', 'dana']);
    expect(rows.map(r => r.status)).toEqual(['alive', 'onTheRopes', 'out', 'out']);
  });

  it('a member whose every pick has covered is won — nothing left to root against', () => {
    const final = [game('BUF', 'MIA', -3, 3, 'final', 27, 20)];
    const rows = buildSurvivorRows([pick('zed', 'BUF'), pick('amy', 'MIA')], final, 1);
    expect(rows.map(r => [r.userName, r.status])).toEqual([['zed', 'won'], ['amy', 'out']]);
  });

  // The top group is everyone you can still root against: games left or in progress.
  it('lists members with games still to play first, then members who already won, then members who are out', () => {
    const later = game('DAL', 'NYG', -3, 3, 'scheduled', null, null, FUTURE);
    const rows = buildSurvivorRows([
      pick('abe', 'BUF'), pick('abe', 'MIA'), // out
      pick('ben', 'BUF'), pick('ben', 'BUF', 'Over'), // won: both final and covered
      pick('cat', 'BUF'), pick('cat', 'KC'), // live, covering
      pick('dan', 'BUF'), pick('dan', 'LV'), // live, not covering
      pick('eve', 'BUF'), // second pick hidden until a later kickoff
    ], [...games, later], 2, 'id-viewer');
    expect(rows.map(r => [r.userName, r.status])).toEqual([
      ['cat', 'alive'], ['dan', 'onTheRopes'], ['eve', 'alive'],
      ['ben', 'won'],
      ['abe', 'out'],
    ]);
  });

  it("the viewer's own unmade pick, with games left, keeps them in the top group", () => {
    const later = game('DAL', 'NYG', -3, 3, 'scheduled', null, null, FUTURE);
    const [row] = buildSurvivorRows([pick('me', 'BUF', 'Spread', 'id-me')], [games[0], later], 2, 'id-me');
    expect(row.status).toBe('alive');
  });
});

describe('formatMargin', () => {
  it('signs positive and negative margins, using a true minus sign', () => {
    expect(formatMargin(3.5)).toBe('+3.5');
    expect(formatMargin(-3.5)).toBe('−3.5');
  });

  it('shows an exact tie as 0', () => {
    expect(formatMargin(0)).toBe('0');
  });
});
