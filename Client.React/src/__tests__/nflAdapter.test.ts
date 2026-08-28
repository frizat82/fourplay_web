import { vi } from 'vitest';
import { createNflAdapter } from '../services/nflAdapter';
import { createCompetition, createScores } from '../test/fixtures';
import type { GameView, SportAdapter } from '../services/sportAdapter';

vi.mock('../api/espn', () => ({
  loadScoresWithRetry: vi.fn(),
  getWeekScores: vi.fn(),
  getLiveGames: vi.fn(),
}));
vi.mock('../api/league', () => ({
  getUserPicks: vi.fn(),
  doOddsExist: vi.fn(),
  spreadBatch: vi.fn(),
  addPicks: vi.fn(),
  getNflCurrentWeek: vi.fn(),
  getLeaguePicks: vi.fn(),
}));
vi.mock('../api/jersey', () => ({ getAllJerseys: vi.fn() }));

import { loadScoresWithRetry, getWeekScores, getLiveGames } from '../api/espn';
import { getUserPicks, doOddsExist, spreadBatch, getNflCurrentWeek, getLeaguePicks } from '../api/league';
import { createSpreadResponse } from '../test/fixtures';

function makeScores(homeTeam: string, awayTeam: string, homeScore = 24, awayScore = 17) {
  const comp = createCompetition({ homeTeam, awayTeam, homeScore, awayScore });
  return createScores({ week: 8, events: [{ id: `${homeTeam}vs${awayTeam}`, season: { year: 2023, type: 2 }, week: { number: 8 }, date: new Date().toISOString(), competitions: [comp] }] });
}

// The control table (NflSeasonWeekConfigs, via SeasonWindowResolver/NflCurrentWeekService, exposed
// as getNflCurrentWeek) is the SOLE source of truth for which week is "current" — it must be
// resolved and then queried BY WEEK (getWeekScores), never inferred from ESPN's own implicit
// "current" scoreboard (loadScoresWithRetry/getScores), which reflects whatever ESPN itself
// considers current (e.g. the most recently completed game during any gap in play) regardless of
// the league's actual spread-release schedule.
const DEFAULT_CURRENT_WEEK = {
  weekId: 8, espnWeek: 8, season: 2023, isPostSeason: false,
  weekLabel: 'Week 8', scoringFormat: 'Standard', spreadLockDatetime: '2023-10-26T17:00:00Z',
};

describe('nflAdapter', () => {
  // getCurrentWeek() caches its resolved answer for the adapter instance's lifetime — a fresh
  // adapter per test avoids one test's mock leaking into the next via that cache.
  let adapter: SportAdapter;

  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(getNflCurrentWeek).mockResolvedValue(DEFAULT_CURRENT_WEEK);
    vi.mocked(getLiveGames).mockResolvedValue([]);
    vi.mocked(getLeaguePicks).mockResolvedValue([]);
    vi.mocked(getUserPicks).mockResolvedValue([]);
    vi.mocked(doOddsExist).mockResolvedValue(false);
    vi.mocked(spreadBatch).mockResolvedValue({ responses: {} });
    adapter = createNflAdapter();
  });

  describe('loadCurrentGames', () => {
    it('maps ESPN competitions to GameView[]', async () => {
      vi.mocked(getWeekScores).mockResolvedValue(makeScores('KC', 'BUF'));
      vi.mocked(doOddsExist).mockResolvedValue(true);
      vi.mocked(spreadBatch).mockResolvedValue({ responses: {
        KC: createSpreadResponse('KC', -3),
        BUF: createSpreadResponse('BUF', 3),
      }});

      const result = await adapter.loadCurrentGames(1, 'user1');

      expect(result.games).toHaveLength(1);
      const game = result.games[0] as GameView;
      expect(game.homeTeam).toBe('KC');
      expect(game.awayTeam).toBe('BUF');
      expect(game.homeScore).toBe(24);
      expect(game.awayScore).toBe(17);
    });

    // frizat: NflSpreads.DateCreated existed on the backend but was never threaded through to
    // the frontend, so "Line posted" only ever showed on the CFB site — cfbAdapter already
    // mapped its equivalent field, nflAdapter silently dropped it.
    it('maps spreadCache dateCreated to spreadPostedAt', async () => {
      vi.mocked(getWeekScores).mockResolvedValue(makeScores('KC', 'BUF'));
      vi.mocked(doOddsExist).mockResolvedValue(true);
      vi.mocked(spreadBatch).mockResolvedValue({ responses: {
        KC: { ...createSpreadResponse('KC', -3), dateCreated: '2026-01-02T12:00:00Z' },
        BUF: { ...createSpreadResponse('BUF', 3), dateCreated: '2026-01-02T12:00:00Z' },
      }});

      const result = await adapter.loadCurrentGames(1, 'user1');

      expect(result.games[0].spreadPostedAt).toBe('2026-01-02T12:00:00Z');
    });

    it('sets hasOdds=true when odds exist', async () => {
      vi.mocked(getWeekScores).mockResolvedValue(makeScores('KC', 'BUF'));
      vi.mocked(doOddsExist).mockResolvedValue(true);

      const result = await adapter.loadCurrentGames(1, 'user1');
      expect(result.hasOdds).toBe(true);
    });

    it('sets hasOdds=false when no odds', async () => {
      vi.mocked(getWeekScores).mockResolvedValue(makeScores('KC', 'BUF'));
      vi.mocked(doOddsExist).mockResolvedValue(false);

      const result = await adapter.loadCurrentGames(1, 'user1');
      expect(result.hasOdds).toBe(false);
    });

    it('maps userPicks to PickView[] with gameId matching game.id', async () => {
      const scores = makeScores('KC', 'BUF');
      const gameId = scores.events![0].competitions[0].id;
      vi.mocked(getWeekScores).mockResolvedValue(scores);
      vi.mocked(doOddsExist).mockResolvedValue(true);
      vi.mocked(getUserPicks).mockResolvedValue([{
        id: 1, leagueId: 1, userId: 'user1', userName: 'Alice',
        team: 'KC', pick: 'Spread' as const, nflWeek: 8, season: 2023, dateCreated: '',
      }]);

      const result = await adapter.loadCurrentGames(1, 'user1');
      expect(result.userPicks).toHaveLength(1);
      expect(result.userPicks[0].team).toBe('KC');
      expect(result.userPicks[0].pickType).toBe('Spread');
      expect(result.userPicks[0].gameId).toBe(gameId);
    });

    // The actual production bug: ESPN's implicit "current" scoreboard has its own idea of what's
    // current (e.g. the last-completed game during any gap in play, such as the summer offseason)
    // which can disagree with the control table entirely. loadCurrentGames must ask ESPN for the
    // control table's specific week (getWeekScores), never touching ESPN's own scoreboard guess
    // (loadScoresWithRetry) — even when that guess is real, populated data.
    it('uses the control-table-resolved week via getWeekScores, ignoring ESPN\'s own "current" scoreboard guess', async () => {
      // ESPN's own scoreboard guess: a stale, unrelated week (e.g. last season's Super Bowl)
      vi.mocked(loadScoresWithRetry).mockResolvedValue(
        createScores({ week: 22, seasonYear: 2022, postSeason: true })
      );
      vi.mocked(getWeekScores).mockResolvedValue(makeScores('KC', 'BUF'));
      vi.mocked(doOddsExist).mockResolvedValue(true);

      const result = await adapter.loadCurrentGames(1, 'user1');

      expect(getWeekScores).toHaveBeenCalledWith(8, 2023, false);
      expect(loadScoresWithRetry).not.toHaveBeenCalled();
      expect(result.season).toBe(2023);
      expect(result.week).toBe(8);
      expect(result.isPostSeason).toBe(false);
    });

    // frizat: NflCurrentWeekService NEVER legitimately resolves to "nothing" — it either returns
    // a real week or throws (no season configs seeded, or a genuine DB/network failure). A
    // swallowed failure here previously rendered as an ordinary empty "no games this week" page,
    // masking a real outage as normal off-season behavior — it must propagate instead, so
    // useQuery's isError -> QueryErrorAlert surfaces it as a real error to the user.
    it('propagates a control-table failure instead of masking it as an empty week', async () => {
      vi.mocked(getNflCurrentWeek).mockRejectedValue(new Error('control table unavailable'));

      await expect(adapter.loadCurrentGames(1, 'user1')).rejects.toThrow('control table unavailable');
      expect(getWeekScores).not.toHaveBeenCalled();
      expect(loadScoresWithRetry).not.toHaveBeenCalled();
    });

    it('returns the resolved week with empty games when getWeekScores has no data', async () => {
      vi.mocked(getWeekScores).mockResolvedValue(null);

      const result = await adapter.loadCurrentGames(1, 'user1');

      expect(result.season).toBe(2023);
      expect(result.week).toBe(8);
      expect(result.games).toEqual([]);
    });
  });

  describe('loadCurrentScores', () => {
    it('uses the control-table-resolved week via getWeekScores, ignoring ESPN\'s own scoreboard guess', async () => {
      vi.mocked(loadScoresWithRetry).mockResolvedValue(
        createScores({ week: 22, seasonYear: 2022, postSeason: true })
      );
      vi.mocked(getWeekScores).mockResolvedValue(makeScores('KC', 'BUF'));
      vi.mocked(doOddsExist).mockResolvedValue(true);

      const result = await adapter.loadCurrentScores(1, 'user1');

      expect(getWeekScores).toHaveBeenCalledWith(8, 2023, false);
      expect(loadScoresWithRetry).not.toHaveBeenCalled();
      expect(result.season).toBe(2023);
      expect(result.week).toBe(8);
      expect(result.hasOdds).toBe(true);
    });

    it('reflects hasOdds=false for the resolved week when no odds have posted yet', async () => {
      vi.mocked(getWeekScores).mockResolvedValue(makeScores('KC', 'BUF'));
      vi.mocked(doOddsExist).mockResolvedValue(false);

      const result = await adapter.loadCurrentScores(1, 'user1');
      expect(result.hasOdds).toBe(false);
    });

    it('propagates a control-table failure instead of masking it as an empty week', async () => {
      vi.mocked(getNflCurrentWeek).mockRejectedValue(new Error('control table unavailable'));

      await expect(adapter.loadCurrentScores(1, 'user1')).rejects.toThrow('control table unavailable');
      expect(getWeekScores).not.toHaveBeenCalled();
      expect(loadScoresWithRetry).not.toHaveBeenCalled();
    });
  });

  describe('config', () => {
    it('has pollIntervalMs > 0', () => {
      expect(adapter.pollIntervalMs).toBeGreaterThan(0);
    });

    // frizat: the control table (getNflCurrentWeek, backed by SeasonWindowResolver) is
    // authoritative for "which season is current" — it must win even when ESPN's own live
    // scoreboard has real, populated data for a different season (e.g. ESPN treats the prior
    // season's Super Bowl as "current" throughout the summer offseason gap).
    it('currentSeasonYear uses the control table, never ESPN\'s own scoreboard', async () => {
      vi.mocked(getNflCurrentWeek).mockResolvedValue({
        weekId: 1, espnWeek: 1, season: 2026, isPostSeason: false,
        weekLabel: 'Week 1', scoringFormat: 'Standard', spreadLockDatetime: '2026-09-09T13:20:00Z',
      });

      const year = await adapter.currentSeasonYear();

      expect(year).toBe(2026);
      expect(loadScoresWithRetry).not.toHaveBeenCalled();
    });

    it('currentSeasonYear propagates a control-table failure rather than defaulting to the calendar year', async () => {
      vi.mocked(getNflCurrentWeek).mockRejectedValue(new Error('control table unavailable'));

      await expect(adapter.currentSeasonYear()).rejects.toThrow('control table unavailable');
      expect(loadScoresWithRetry).not.toHaveBeenCalled();
    });
  });
});
