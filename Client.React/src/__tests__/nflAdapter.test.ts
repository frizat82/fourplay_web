import { vi } from 'vitest';
import { createNflAdapter } from '../services/nflAdapter';
import { createCompetition, createScores } from '../test/fixtures';
import type { GameView, SportAdapter } from '../services/sportAdapter';

vi.mock('../api/espn', () => ({
  getScores: vi.fn(),
  getWeekScores: vi.fn(),
  getLiveGames: vi.fn(),
}));
vi.mock('../api/league', () => ({
  getUserPicks: vi.fn(),
  spreadBatch: vi.fn(),
  addPicks: vi.fn(),
  removeMyPick: vi.fn(),
  getNflCurrentWeek: vi.fn(),
  getLeaguePicks: vi.fn(),
  getLeaguePickCounts: vi.fn(),
}));
vi.mock('../api/jersey', () => ({ getAllJerseys: vi.fn() }));

import { getScores, getWeekScores, getLiveGames } from '../api/espn';
import { getUserPicks, spreadBatch, getNflCurrentWeek, getLeaguePicks, getLeaguePickCounts } from '../api/league';
import { createSpreadResponse, notFoundError } from '../test/fixtures';

function makeScores(homeTeam: string, awayTeam: string, homeScore = 24, awayScore = 17) {
  const comp = createCompetition({ homeTeam, awayTeam, homeScore, awayScore });
  return createScores({ week: 8, events: [{ id: `${homeTeam}vs${awayTeam}`, season: { year: 2023, type: 2 }, week: { number: 8 }, date: new Date().toISOString(), competitions: [comp] }] });
}

// The control table (NflSeasonWeekConfigs, via SeasonWindowResolver/NflCurrentWeekService, exposed
// as getNflCurrentWeek) is the SOLE source of truth for which week is "current" — it must be
// resolved and then queried BY WEEK (getWeekScores), never inferred from ESPN's own implicit
// "current" scoreboard (getScores), which reflects whatever ESPN itself
// considers current (e.g. the most recently completed game during any gap in play) regardless of
// the league's actual spread-release schedule.
const DEFAULT_CURRENT_WEEK = {
  weekId: 8, season: 2023, isPostSeason: false,
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
    vi.mocked(getLeaguePickCounts).mockResolvedValue([]);
    vi.mocked(getUserPicks).mockResolvedValue([]);
    // Default: no odds posted — the spreads endpoint 404s (nflAdapter derives hasOdds from it).
    vi.mocked(spreadBatch).mockRejectedValue(notFoundError());
    adapter = createNflAdapter();
  });

  describe('loadCurrentGames', () => {
    it('maps ESPN competitions to GameView[]', async () => {
      vi.mocked(getWeekScores).mockResolvedValue(makeScores('KC', 'BUF'));
      vi.mocked(spreadBatch).mockResolvedValue({ responses: {} });
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
      vi.mocked(spreadBatch).mockResolvedValue({ responses: {} });
      vi.mocked(spreadBatch).mockResolvedValue({ responses: {
        KC: { ...createSpreadResponse('KC', -3), dateCreated: '2026-01-02T12:00:00Z' },
        BUF: { ...createSpreadResponse('BUF', 3), dateCreated: '2026-01-02T12:00:00Z' },
      }});

      const result = await adapter.loadCurrentGames(1, 'user1');

      expect(result.games[0].spreadPostedAt).toBe('2026-01-02T12:00:00Z');
    });

    it('sets hasOdds=true when odds exist', async () => {
      vi.mocked(getWeekScores).mockResolvedValue(makeScores('KC', 'BUF'));
      vi.mocked(spreadBatch).mockResolvedValue({ responses: {} });

      const result = await adapter.loadCurrentGames(1, 'user1');
      expect(result.hasOdds).toBe(true);
    });

    it('sets hasOdds=false when no odds', async () => {
      vi.mocked(getWeekScores).mockResolvedValue(makeScores('KC', 'BUF'));
      vi.mocked(spreadBatch).mockRejectedValue(notFoundError());

      const result = await adapter.loadCurrentGames(1, 'user1');
      expect(result.hasOdds).toBe(false);
    });

    it('maps userPicks to PickView[] with gameId matching game.id', async () => {
      const scores = makeScores('KC', 'BUF');
      const gameId = scores.events![0].competitions[0].id;
      vi.mocked(getWeekScores).mockResolvedValue(scores);
      vi.mocked(spreadBatch).mockResolvedValue({ responses: {} });
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
    // (getScores) — even when that guess is real, populated data.
    it('uses the control-table-resolved week via getWeekScores, ignoring ESPN\'s own "current" scoreboard guess', async () => {
      // ESPN's own scoreboard guess: a stale, unrelated week (e.g. last season's Super Bowl)
      vi.mocked(getScores).mockResolvedValue(
        createScores({ week: 22, seasonYear: 2022, postSeason: true })
      );
      vi.mocked(getWeekScores).mockResolvedValue(makeScores('KC', 'BUF'));
      vi.mocked(spreadBatch).mockResolvedValue({ responses: {} });

      const result = await adapter.loadCurrentGames(1, 'user1');

      expect(getWeekScores).toHaveBeenCalledWith(2023, 8);
      expect(getScores).not.toHaveBeenCalled();
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
      expect(getScores).not.toHaveBeenCalled();
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
      vi.mocked(getScores).mockResolvedValue(
        createScores({ week: 22, seasonYear: 2022, postSeason: true })
      );
      vi.mocked(getWeekScores).mockResolvedValue(makeScores('KC', 'BUF'));
      vi.mocked(spreadBatch).mockResolvedValue({ responses: {} });

      const result = await adapter.loadCurrentScores(1, 'user1');

      expect(getWeekScores).toHaveBeenCalledWith(2023, 8);
      expect(getScores).not.toHaveBeenCalled();
      expect(result.season).toBe(2023);
      expect(result.week).toBe(8);
      expect(result.hasOdds).toBe(true);
    });

    it('reflects hasOdds=false for the resolved week when no odds have posted yet', async () => {
      vi.mocked(getWeekScores).mockResolvedValue(makeScores('KC', 'BUF'));
      vi.mocked(spreadBatch).mockRejectedValue(notFoundError());

      const result = await adapter.loadCurrentScores(1, 'user1');
      expect(result.hasOdds).toBe(false);
    });

    it('propagates a control-table failure instead of masking it as an empty week', async () => {
      vi.mocked(getNflCurrentWeek).mockRejectedValue(new Error('control table unavailable'));

      await expect(adapter.loadCurrentScores(1, 'user1')).rejects.toThrow('control table unavailable');
      expect(getWeekScores).not.toHaveBeenCalled();
      expect(getScores).not.toHaveBeenCalled();
    });
  });

  // frizat-66c: when ESPN's live feed gives period/displayClock (e.g. "Q3") but the fuller
  // situation sub-object is momentarily absent (a real, apparently common gap in ESPN's own live
  // payload — between snaps, right after a play, etc.), the old code fabricated a full placeholder
  // situation object (yardLine: 0, downDistanceText: '') merged with the real period/clock — so
  // FieldPosition rendered a ball at a fake position with a blank down-distance caption instead of
  // rendering nothing. CFB's cfbAdapter.ts never had this bug (fetchCfbEspnData only merges
  // period/clock onto a REAL situation, returning null otherwise) — this mirrors that exact,
  // already-correct behavior so NFL and CFB share one shape again.
  describe('situation mapping', () => {
    // situation (ball/down-distance, FieldPosition) and period/displayClock (status line) are
    // two independently-nullable concerns — a real ESPN gap at halftime (no active down, so no
    // situation detail) must not also blank out the "Q2 0:00" status line, which only needs
    // LiveGame's own top-level period/displayClock, not anything nested inside situation.
    it('does not fabricate a placeholder situation when ESPN gives period/clock but no full situation detail — but still surfaces period/displayClock for the status line', async () => {
      vi.mocked(getWeekScores).mockResolvedValue(makeScores('KC', 'BUF'));
      vi.mocked(spreadBatch).mockRejectedValue(notFoundError());
      vi.mocked(getLiveGames).mockResolvedValue([{
        homeTeam: 'KC', awayTeam: 'BUF', homeScore: 24, awayScore: 17,
        isCompleted: false, kickoffUtc: new Date().toISOString(),
        situation: null, period: 2, displayClock: '0:00',
      }]);

      const result = await adapter.loadCurrentScores(1, 'user1');

      expect(result.games[0].situation).toBeNull();
      expect(result.games[0].period).toBe(2);
      expect(result.games[0].displayClock).toBe('0:00');
    });

    it('surfaces a real situation object unmodified, alongside period/displayClock as separate fields', async () => {
      vi.mocked(getWeekScores).mockResolvedValue(makeScores('KC', 'BUF'));
      vi.mocked(spreadBatch).mockRejectedValue(notFoundError());
      vi.mocked(getLiveGames).mockResolvedValue([{
        homeTeam: 'KC', awayTeam: 'BUF', homeScore: 24, awayScore: 17,
        isCompleted: false, kickoffUtc: new Date().toISOString(),
        situation: {
          possessionTeam: 'KC', isHomePossession: true, yardLine: 35,
          down: 3, distance: 7, isRedZone: false, downDistanceText: '3rd & 7 at KC 35',
        },
        period: 3, displayClock: '8:42',
      }]);

      const result = await adapter.loadCurrentScores(1, 'user1');

      expect(result.games[0].situation).toEqual({
        possessionTeam: 'KC', isHomePossession: true, yardLine: 35,
        down: 3, distance: 7, isRedZone: false, downDistanceText: '3rd & 7 at KC 35',
      });
      expect(result.games[0].period).toBe(3);
      expect(result.games[0].displayClock).toBe('8:42');
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
        weekId: 1, season: 2026, isPostSeason: false,
        weekLabel: 'Week 1', scoringFormat: 'Standard', spreadLockDatetime: '2026-09-09T13:20:00Z',
      });

      const year = await adapter.currentSeasonYear();

      expect(year).toBe(2026);
      expect(getScores).not.toHaveBeenCalled();
    });

    it('currentSeasonYear propagates a control-table failure rather than defaulting to the calendar year', async () => {
      vi.mocked(getNflCurrentWeek).mockRejectedValue(new Error('control table unavailable'));

      await expect(adapter.currentSeasonYear()).rejects.toThrow('control table unavailable');
      expect(getScores).not.toHaveBeenCalled();
    });
  });

  // frizat-8ni: backs the commissioner missing-picks view (frizat-6sc) via the adapter, reusing
  // the same memoized getCurrentWeek() resolver loadCurrentGames/loadCurrentScores already use.
  //
  // frizat-xbq: submitted counts come from the count endpoint, not the visible-picks one.
  describe('getMissingPicks', () => {
    it('resolves picksByUser and requiredPicks for the current week from the submitted-count endpoint, never the visible-picks one', async () => {
      vi.mocked(getLeaguePickCounts).mockResolvedValue([
        { userId: 'alice', pickCount: 2 },
        { userId: 'bob', pickCount: 1 },
      ]);

      const result = await adapter.getMissingPicks(1);

      expect(result.requiredPicks).toBe(4); // DEFAULT_CURRENT_WEEK.weekId=8, regular season
      expect(result.picksByUser.get('alice')).toBe(2);
      expect(result.picksByUser.get('bob')).toBe(1);
      expect(getLeaguePickCounts).toHaveBeenCalledWith(1, 2023, 8);
      // The regression (frizat-xbq): that endpoint hides other users' picks on games that haven't
      // kicked off, so a member's 4th pick on tonight's game was invisible to the commissioner.
      expect(getLeaguePicks).not.toHaveBeenCalled();
    });


    // frizat-4bv: the Members tab's "This Week" column header shows this instead of a generic
    // static label — reuses getCurrentWeek()'s own weekLabel rather than re-deriving one.
    it('includes the resolved current week\'s human-readable label', async () => {
      const result = await adapter.getMissingPicks(1);

      expect(result.weekLabel).toBe('Week 8'); // DEFAULT_CURRENT_WEEK.weekLabel
    });

    // Same memoized getCurrentWeek() instance loadCurrentGames/loadCurrentScores use — must not
    // re-resolve "what's current" from scratch as a second, competing fetch (frizat-8ni).
    it('reuses the adapter\'s memoized current-week resolution — does not re-fetch it', async () => {
      await adapter.currentSeasonYear(); // resolves and caches getCurrentWeek()
      vi.mocked(getNflCurrentWeek).mockClear();
      await adapter.getMissingPicks(1);

      expect(getNflCurrentWeek).not.toHaveBeenCalled();
    });
  });

  // Fired as soon as an adapter-driven route mounts, so the current-week request runs alongside
  // the session's league fetch instead of waiting for it (one fewer round trip on cold load).
  describe('prefetchCurrentWeek', () => {
    it('starts the current-week fetch, and loadCurrentGames reuses it', async () => {
      adapter.prefetchCurrentWeek();
      expect(getNflCurrentWeek).toHaveBeenCalledTimes(1);

      await adapter.loadCurrentGames(1, 'user-1');
      expect(getNflCurrentWeek).toHaveBeenCalledTimes(1);
    });

    it('swallows a failed prefetch and lets the real load retry and surface the error', async () => {
      vi.mocked(getNflCurrentWeek).mockRejectedValueOnce(new Error('control table unavailable'));
      adapter.prefetchCurrentWeek();
      await Promise.resolve();

      await expect(adapter.loadCurrentGames(1, 'user-1')).resolves.toBeDefined();
      expect(getNflCurrentWeek).toHaveBeenCalledTimes(2);
    });
  });

  // Request-chain depth: nothing the NFL pages need depends on the ESPN scoreboard's response
  // except the final merge, so every request goes out together (matching cfbAdapter's shape) —
  // spreads used to wait on the scoreboard just for its team list, and live games / past weeks
  // waited on requests they never used.
  describe('parallel loading', () => {
    function deferred<T>() {
      let resolve!: (value: T) => void;
      const promise = new Promise<T>(r => { resolve = r; });
      return { promise, resolve };
    }

    it('loadCurrentGames requests every team\'s spreads alongside the scoreboard, not after it', async () => {
      const scores = deferred<ReturnType<typeof makeScores>>();
      vi.mocked(getWeekScores).mockReturnValue(scores.promise);
      vi.mocked(spreadBatch).mockResolvedValue({ responses: {} });
      vi.mocked(spreadBatch).mockResolvedValue({ responses: { KC: createSpreadResponse('KC', -3), BUF: createSpreadResponse('BUF', 3) } });

      const pending = adapter.loadCurrentGames(1, 'user1');
      await vi.waitFor(() => expect(spreadBatch).toHaveBeenCalledWith(1, 2023, 8, { requests: [] }));
      scores.resolve(makeScores('KC', 'BUF'));
      const result = await pending;

      expect(result.games[0].homeSpread).toBe(-3);
      expect(spreadBatch).toHaveBeenCalledTimes(1);
    });

    // Deploy skew: a backend that predates the all-teams request answers it with nothing — fall
    // back to asking for the scoreboard's teams rather than show a week with odds but no spreads.
    it('falls back to the scoreboard\'s team list when the all-teams request comes back empty', async () => {
      vi.mocked(getWeekScores).mockResolvedValue(makeScores('KC', 'BUF'));
      vi.mocked(spreadBatch).mockResolvedValue({ responses: {} });
      vi.mocked(spreadBatch)
        .mockResolvedValueOnce({ responses: {} })
        .mockResolvedValueOnce({ responses: { KC: createSpreadResponse('KC', -3), BUF: createSpreadResponse('BUF', 3) } });

      const result = await adapter.loadCurrentGames(1, 'user1');

      expect(spreadBatch).toHaveBeenLastCalledWith(1, 2023, 8, { requests: [{ team: 'KC' }, { team: 'BUF' }] });
      expect(result.games[0].homeSpread).toBe(-3);
    });

    // The spreads request doubles as the "have odds posted?" check (there is no /odds/exists call):
    // its 404 means no odds yet; any other failure is a real error, not a silent "no odds".
    it('treats a 404 from the spreads request as no odds, and any other failure as an error', async () => {
      vi.mocked(getWeekScores).mockResolvedValue(makeScores('KC', 'BUF'));
      vi.mocked(spreadBatch).mockRejectedValue(notFoundError());

      const result = await adapter.loadCurrentGames(1, 'user1');
      expect(result.hasOdds).toBe(false);
      expect(result.games).toHaveLength(1);

      vi.mocked(spreadBatch).mockRejectedValue(new Error('Network Error'));
      await expect(adapter.loadCurrentGames(1, 'user1')).rejects.toThrow('Network Error');
    });

    it('loadCurrentScores requests live games alongside the scoreboard, not after it', async () => {
      const scores = deferred<ReturnType<typeof makeScores>>();
      vi.mocked(getWeekScores).mockReturnValue(scores.promise);

      const pending = adapter.loadCurrentScores(1, 'user1');
      await vi.waitFor(() => expect(getLiveGames).toHaveBeenCalled());
      scores.resolve(makeScores('KC', 'BUF'));
      await pending;
    });

    it('loadHistoricalScores fetches the requested week without waiting on the current scoreboard', async () => {
      const current = deferred<ReturnType<typeof makeScores>>();
      vi.mocked(getScores).mockReturnValue(current.promise);
      vi.mocked(getWeekScores).mockResolvedValue(makeScores('KC', 'BUF'));

      const pending = adapter.loadHistoricalScores(1, 'user1', { season: 2023, week: 5, isPostSeason: false });
      await vi.waitFor(() => expect(getWeekScores).toHaveBeenCalledWith(2023, 5));
      expect(getLeaguePicks).toHaveBeenCalledWith(1, 2023, 5);
      current.resolve(makeScores('SF', 'DAL')); // a different (current) week — not the one asked for
      await pending;
    });

    it('loadHistoricalGames fetches the requested week without waiting on the current scoreboard', async () => {
      const current = deferred<ReturnType<typeof makeScores>>();
      vi.mocked(getScores).mockReturnValue(current.promise);
      vi.mocked(getWeekScores).mockResolvedValue(makeScores('KC', 'BUF'));

      const pending = adapter.loadHistoricalGames(1, 'user1', { season: 2023, week: 5, isPostSeason: false });
      await vi.waitFor(() => expect(getWeekScores).toHaveBeenCalledWith(2023, 5));
      expect(getUserPicks).toHaveBeenCalledWith('user1', 1, 2023, 5);
      current.resolve(makeScores('SF', 'DAL')); // a different (current) week — not the one asked for
      await pending;
    });

    // An empty week (no scoreboard events) still resolves to null even though the week's picks /
    // odds / spreads are now requested alongside it — their failure mustn't turn "nothing here"
    // into an error page.
    it('past-week loaders still return null for an empty week when the parallel requests fail', async () => {
      vi.mocked(getScores).mockResolvedValue(makeScores('SF', 'DAL'));
      vi.mocked(getWeekScores).mockResolvedValue(null);
      vi.mocked(getUserPicks).mockRejectedValue(new Error('403'));
      vi.mocked(getLeaguePicks).mockRejectedValue(new Error('403'));
      vi.mocked(spreadBatch).mockRejectedValue(new Error('500'));

      await expect(adapter.loadHistoricalGames(1, 'user1', { season: 2023, week: 5, isPostSeason: false })).resolves.toBeNull();
      await expect(adapter.loadHistoricalScores(1, 'user1', { season: 2023, week: 5, isPostSeason: false })).resolves.toBeNull();
    });
  });
});
