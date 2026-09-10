import { describe, it, expect } from 'vitest';
import { mergeLiveSituation } from '../utils/gameHelpers';
import type { LiveGame, GameSituation } from '../types/liveGame';

function buildLiveGame(overrides?: Partial<LiveGame>): LiveGame {
  return {
    homeTeam: 'KC', awayTeam: 'BUF', homeScore: 24, awayScore: 17,
    isCompleted: false, kickoffUtc: new Date().toISOString(),
    situation: null,
    ...overrides,
  };
}

function buildSituation(overrides?: Partial<GameSituation>): GameSituation {
  return {
    possessionTeam: 'KC', isHomePossession: true, yardLine: 35,
    down: 3, distance: 7, isRedZone: false, downDistanceText: '3rd & 7 at KC 35',
    ...overrides,
  };
}

// frizat-66c: shared by nflAdapter.ts and cfbAdapter.ts (both previously duplicated this exact
// merge logic — CFB always had it right, NFL used to fabricate a placeholder situation whenever
// ESPN gave period/clock but no full situation detail). One implementation now.
describe('mergeLiveSituation', () => {
  it('returns null for a null/undefined LiveGame', () => {
    expect(mergeLiveSituation(null)).toBeNull();
    expect(mergeLiveSituation(undefined)).toBeNull();
  });

  it('does not fabricate a placeholder when situation is null, even if period/displayClock are present', () => {
    const live = buildLiveGame({ situation: null, period: 3, displayClock: '8:42' });
    expect(mergeLiveSituation(live)).toBeNull();
  });

  it('merges period/displayClock onto a real situation without altering its own fields', () => {
    const live = buildLiveGame({ situation: buildSituation(), period: 3, displayClock: '8:42' });

    expect(mergeLiveSituation(live)).toEqual({
      ...buildSituation(),
      period: 3,
      displayClock: '8:42',
    });
  });
});
