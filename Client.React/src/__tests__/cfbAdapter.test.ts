import { vi } from 'vitest';
import { createCfbAdapter } from '../services/cfbAdapter';
import type { CfbSlateDto, CfbSpreadDto, CfbPickDto } from '../types/league';
import type { EspnScores } from '../types/espn';

vi.mock('../api/cfb', () => ({
  getCfbCurrentSlate: vi.fn(),
  getCfbSlates: vi.fn(),
  getCfbSpreads: vi.fn(),
  getCfbScores: vi.fn(),
  getCfbUserPicks: vi.fn(),
  getCfbAllPicks: vi.fn(),
  addCfbPicks: vi.fn(),
  deleteCfbPicks: vi.fn(),
}));

vi.mock('../api/espn', () => ({
  getCfbScoresForSlate: vi.fn(),
  getCfbLiveGames: vi.fn(),
}));

import { getCfbCurrentSlate, getCfbSlates, getCfbSpreads, getCfbScores, getCfbUserPicks, getCfbAllPicks, addCfbPicks } from '../api/cfb';
import { getCfbScoresForSlate, getCfbLiveGames } from '../api/espn';

const slate: CfbSlateDto = {
  id: 10, season: 2026, slateNumber: 8, label: 'Week 8',
  slateType: 'RegularSeason', startDate: '2026-10-20', endDate: '2026-10-26',
};
// Over/Under are deliberately asymmetric (not a mirror pair) — the backend juices each
// independently (SpreadCalculator.GetOverUnder), so this fixture must not use equal values,
// otherwise a regression that collapses them back into one shared number would go unnoticed.
const spread: CfbSpreadDto = {
  id: 1, cfbSlateId: 10, homeTeam: 'MICH', awayTeam: 'PSU',
  homeTeamSpread: -3.5, awayTeamSpread: 3.5, over: 40.5, under: 48.5,
  gameTime: '2025-10-11T20:00:00Z', dateCreated: '2025-10-09T14:00:00Z',
  homeTeamRank: 5, awayTeamRank: null,
};

/**
 * Minimal EspnScores with one final game matching espnEventId=999.
 * status.type.name is the numeric wire form (0=final) — our backend re-serializes the ESPN
 * status enum as a plain number (no JsonStringEnumConverter), never the raw "STATUS_FINAL"
 * string a live ESPN response would use. See gameHelpers.ts's isStatus() / isGameOver().
 */
const espnFinalGame: EspnScores = {
  leagues: [], season: { year: 2026, type: 2 }, week: { number: 8 },
  events: [{
    id: '999', date: '2026-10-24T20:00:00Z',
    season: { year: 2026, type: 2 }, week: { number: 8 },
    competitions: [{
      id: '999', date: '2025-10-11T20:00:00Z',
      status: { type: { id: 3, name: 0, completed: true, description: 'Final', state: 'post', detail: 'Final', shortDetail: 'Final' }, clock: 0, period: 4, displayClock: '0:00' },
      competitors: [
        { id: 'mich', homeAway: 'home' as const, score: 27, team: { abbreviation: 'MICH', logo: '' }, records: [] },
        { id: 'psu', homeAway: 'away' as const, score: 13, team: { abbreviation: 'PSU', logo: '' }, records: [] },
      ],
      odds: [], situation: null,
    }],
    weather: null,
  }],
};

const adapter = createCfbAdapter();

describe('cfbAdapter', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(getCfbCurrentSlate).mockResolvedValue(slate);
    vi.mocked(getCfbLiveGames).mockResolvedValue([]);
    vi.mocked(getCfbScores).mockResolvedValue([]);
  });

  describe('loadCurrentGames', () => {
    it('maps CfbSpreadDto + ESPN live data to GameView[]', async () => {
      vi.mocked(getCfbSlates).mockResolvedValue([slate]);
      vi.mocked(getCfbSpreads).mockResolvedValue([spread]);
      vi.mocked(getCfbScoresForSlate).mockResolvedValue(espnFinalGame);
      vi.mocked(getCfbUserPicks).mockResolvedValue([]);

      const result = await adapter.loadCurrentGames(1, 'user1');

      expect(result.games).toHaveLength(1);
      const game = result.games[0];
      expect(game.homeTeam).toBe('MICH');
      expect(game.awayTeam).toBe('PSU');
      expect(game.homeSpread).toBe(-3.5);
      expect(game.awaySpread).toBe(3.5);
      expect(game.overThreshold).toBe(40.5);
      expect(game.underThreshold).toBe(48.5);
      expect(game.homeScore).toBe(27);
      expect(game.awayScore).toBe(13);
      expect(game.gameStatus).toBe('final');
      expect(game.id).toBe('MICH');
      expect(game.spreadPostedAt).toBe('2025-10-09T14:00:00Z');
      expect(game.homeRank).toBe(5);
      expect(game.awayRank).toBeNull();
    });

    it('always sets hasOdds=true when spreads exist', async () => {
      vi.mocked(getCfbSlates).mockResolvedValue([slate]);
      vi.mocked(getCfbSpreads).mockResolvedValue([spread]);
      vi.mocked(getCfbScoresForSlate).mockResolvedValue(null);
      vi.mocked(getCfbUserPicks).mockResolvedValue([]);

      const result = await adapter.loadCurrentGames(1, 'user1');
      expect(result.hasOdds).toBe(true);
    });

    it('sets hasOdds=false when no spreads exist', async () => {
      vi.mocked(getCfbSlates).mockResolvedValue([slate]);
      vi.mocked(getCfbSpreads).mockResolvedValue([]);
      vi.mocked(getCfbScoresForSlate).mockResolvedValue(null);
      vi.mocked(getCfbUserPicks).mockResolvedValue([]);

      const result = await adapter.loadCurrentGames(1, 'user1');
      expect(result.hasOdds).toBe(false);
    });

    it('maps a home-team CfbPickDto to PickView with gameId = homeTeam', async () => {
      const pick: CfbPickDto = {
        id: 1, userId: 'user1', userName: 'user1', leagueId: 1, cfbSlateId: 10,
        team: 'MICH', pickType: 'Spread', season: 2026,
      };
      vi.mocked(getCfbSlates).mockResolvedValue([slate]);
      vi.mocked(getCfbSpreads).mockResolvedValue([spread]);
      vi.mocked(getCfbScoresForSlate).mockResolvedValue(espnFinalGame);
      vi.mocked(getCfbUserPicks).mockResolvedValue([pick]);

      const result = await adapter.loadCurrentGames(1, 'user1');
      expect(result.userPicks).toHaveLength(1);
      expect(result.userPicks[0].gameId).toBe('MICH');
      expect(result.userPicks[0].team).toBe('MICH');
      expect(result.userPicks[0].pickType).toBe('Spread');
    });

    // frizat: CfbPicks.Team can be the AWAY side — since GameView.id is always the game's home
    // team (a team plays at most one game per slate, so homeTeam alone is the join key), a pick
    // on the away side must still resolve gameId back to the home team, not to pick.team itself,
    // or it would never match GameView.id in ScoresPage's pickCountForTeam/didUserPick lookups.
    it('maps an away-team CfbPickDto to PickView with gameId = the game\'s homeTeam, not the picked team', async () => {
      const pick: CfbPickDto = {
        id: 2, userId: 'user1', userName: 'user1', leagueId: 1, cfbSlateId: 10,
        team: 'PSU', pickType: 'Spread', season: 2026,
      };
      vi.mocked(getCfbSlates).mockResolvedValue([slate]);
      vi.mocked(getCfbSpreads).mockResolvedValue([spread]);
      vi.mocked(getCfbScoresForSlate).mockResolvedValue(espnFinalGame);
      vi.mocked(getCfbUserPicks).mockResolvedValue([pick]);

      const result = await adapter.loadCurrentGames(1, 'user1');
      expect(result.userPicks).toHaveLength(1);
      expect(result.userPicks[0].gameId).toBe('MICH');
      expect(result.userPicks[0].team).toBe('PSU');
    });

    it('derives WeekState from slate slateNumber', async () => {
      vi.mocked(getCfbSlates).mockResolvedValue([slate]); // slateNumber=8
      vi.mocked(getCfbSpreads).mockResolvedValue([]);
      vi.mocked(getCfbScoresForSlate).mockResolvedValue(null);
      vi.mocked(getCfbUserPicks).mockResolvedValue([]);

      const result = await adapter.loadCurrentGames(1, 'user1');
      expect(result.week).toBe(8);
      expect(result.isPostSeason).toBe(false);
      expect(result.season).toBe(2026);
    });

    // frizat-8y4: maxWeek used to be computed by scanning ALL pre-seeded RegularSeason slates
    // for the season (CfbSlateSeederJob seeds the whole season up front, unlike NFL's NflScores
    // rows which only exist once a game is final) — so it reflected the season's total length,
    // not the real current week, letting Next walk all the way to the last regular-season slate
    // regardless of which week is actually live. loadCurrentScores already got this right via
    // maxWeek: weekState.week; loadCurrentGames must match it.
    it('caps maxWeek at the real current week, not the season-wide last pre-seeded slate', async () => {
      const futureSlates: CfbSlateDto[] = [9, 10, 11, 12, 13].map(slateNumber => ({
        id: slateNumber, season: 2026, slateNumber, label: `Week ${slateNumber}`,
        slateType: 'RegularSeason', startDate: '2026-11-01', endDate: '2026-11-07',
      }));
      vi.mocked(getCfbSlates).mockResolvedValue([slate, ...futureSlates]); // current slateNumber=8
      vi.mocked(getCfbSpreads).mockResolvedValue([]);
      vi.mocked(getCfbScoresForSlate).mockResolvedValue(null);
      vi.mocked(getCfbUserPicks).mockResolvedValue([]);

      const result = await adapter.loadCurrentGames(1, 'user1');
      expect(result.maxWeek).toBe(8);
    });

    // frizat-8y7: caught by CI right after the frizat-8y4 fix landed — maxWeek: weekState.week
    // doesn't distinguish regular season from postseason. When the current slate IS postseason
    // (e.g. CFP Championship, slateNumber 18 -> postseason week 5), that small postseason week
    // number leaked in as the REGULAR SEASON cap instead of the real regular-season length (13)
    // — collapsing the regular-season selector down to 5 options instead of all 13 the moment
    // navigation was no longer hidden behind the old static regularWeekOptions override.
    it('caps maxWeek at the full regular-season length when the current slate is postseason', async () => {
      // Fresh adapter — createCfbAdapter() wraps getCfbCurrentSlate in memoizeOnce, and the
      // module-level `adapter` shared by every other test in this file has already cached its
      // own (regular-season) "current slate" by the time this test runs.
      const freshAdapter = createCfbAdapter();
      const postseasonSlate: CfbSlateDto = {
        id: 18, season: 2026, slateNumber: 18, label: 'CFP Championship',
        slateType: 'Postseason', startDate: '2027-01-01', endDate: '2027-01-07',
      };
      vi.mocked(getCfbCurrentSlate).mockResolvedValue(postseasonSlate);
      vi.mocked(getCfbSlates).mockResolvedValue([postseasonSlate]);
      vi.mocked(getCfbSpreads).mockResolvedValue([]);
      vi.mocked(getCfbScoresForSlate).mockResolvedValue(null);
      vi.mocked(getCfbUserPicks).mockResolvedValue([]);

      const result = await freshAdapter.loadCurrentGames(1, 'user1');
      expect(result.isPostSeason).toBe(true);
      expect(result.maxWeek).toBe(13);
    });

    it('game shows scheduled when ESPN has no matching event', async () => {
      vi.mocked(getCfbSlates).mockResolvedValue([slate]);
      vi.mocked(getCfbSpreads).mockResolvedValue([spread]);
      vi.mocked(getCfbScoresForSlate).mockResolvedValue({ leagues: [], season: { year: 2026, type: 2 }, week: { number: 8 }, events: [] });
      vi.mocked(getCfbUserPicks).mockResolvedValue([]);

      const result = await adapter.loadCurrentGames(1, 'user1');
      expect(result.games[0].gameStatus).toBe('scheduled');
      expect(result.games[0].homeScore).toBeNull();
    });
  });

  describe('loadCurrentScores', () => {
    // frizat-8y7: same bug as loadCurrentGames — maxWeek: weekState.week doesn't distinguish
    // regular season from postseason, so a postseason current slate (e.g. CFP Championship)
    // leaked its small postseason week number in as the regular-season cap.
    it('caps maxWeek at the full regular-season length when the current slate is postseason', async () => {
      // Fresh adapter — see the identical note in the loadCurrentGames test above.
      const freshAdapter = createCfbAdapter();
      const postseasonSlate: CfbSlateDto = {
        id: 18, season: 2026, slateNumber: 18, label: 'CFP Championship',
        slateType: 'Postseason', startDate: '2027-01-01', endDate: '2027-01-07',
      };
      vi.mocked(getCfbCurrentSlate).mockResolvedValue(postseasonSlate);
      vi.mocked(getCfbSpreads).mockResolvedValue([]);
      vi.mocked(getCfbScoresForSlate).mockResolvedValue(null);
      vi.mocked(getCfbAllPicks).mockResolvedValue([]);
      vi.mocked(getCfbUserPicks).mockResolvedValue([]);

      const result = await freshAdapter.loadCurrentScores(1, 'user1');
      expect(result.isPostSeason).toBe(true);
      expect(result.maxWeek).toBe(13);
    });
  });

  describe('submitPicks', () => {
    // frizat: the request payload no longer carries an ESPN event id — just team + pickType,
    // matching NFL's submitPicks shape exactly (no per-pick game-id field at all).
    it('sends only team and pickType, no espnEventId, per pick', async () => {
      vi.mocked(getCfbSlates).mockResolvedValue([slate]);
      vi.mocked(addCfbPicks).mockResolvedValue({ added: 1 });

      await adapter.submitPicks(1, { season: 2026, week: 8, isPostSeason: false }, [
        { gameId: 'MICH', team: 'MICH', pickType: 'Spread' },
      ]);

      expect(addCfbPicks).toHaveBeenCalledWith(1, slate.id, 2026, [
        { team: 'MICH', pickType: 'Spread' },
      ]);
    });
  });

  describe('config', () => {
    it('pollIntervalMs is 300s (SSE primary; poll is fallback)', () => {
      expect(adapter.pollIntervalMs).toBe(300_000);
    });
    it('currentSeasonYear returns 2026', async () => {
      expect(await adapter.currentSeasonYear()).toBe(2026);
    });
    it('sseUrl points at CFB live-stream endpoint', () => {
      expect(adapter.sseUrl).toBeDefined();
      expect(adapter.sseUrl).toContain('/api/cfb/live-stream');
    });
    it('weekLabelFn returns CFP Championship for week 5 postseason', () => {
      const fn = adapter.weekSelectorConfig.weekLabelFn!;
      expect(fn(5, true)).toBe('CFP Championship');
      expect(() => fn(5, true)).not.toThrow();
    });

    // frizat-8y4: WeekYearSelector prioritizes a non-empty regularWeekOptions unconditionally
    // over the dynamic maxRegularSeasonWeek prop PicksPage/ScoresPage pass in per-load — a
    // static full-season regularWeekOptions here silently made maxWeek/maxRegularSeasonWeek
    // inert for capping regular-season navigation, so Next stayed clickable through the whole
    // season regardless of which week was actually current. Must stay unset so
    // WeekYearSelector's dynamic fallback (built from maxRegularSeasonWeek) governs instead —
    // the same mechanism nflAdapter.ts (which never sets this either) already relies on.
    it('does not set a static regularWeekOptions, so the dynamic maxWeek/maxRegularSeasonWeek prop actually caps navigation', () => {
      expect(adapter.weekSelectorConfig.regularWeekOptions).toBeUndefined();
    });
  });
});
