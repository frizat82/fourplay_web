import { vi } from 'vitest';
import { createNflAdapter } from '../services/nflAdapter';
import { createCompetition, createScores } from '../test/fixtures';
import type { GameView } from '../services/sportAdapter';

vi.mock('../api/espn', () => ({
  loadScoresWithRetry: vi.fn(),
  getWeekScores: vi.fn(),
}));
vi.mock('../api/league', () => ({
  getUserPicks: vi.fn(),
  doOddsExist: vi.fn(),
  spreadBatch: vi.fn(),
  addPicks: vi.fn(),
  getNflCurrentWeek: vi.fn(),
}));
vi.mock('../api/jersey', () => ({ getAllJerseys: vi.fn() }));

import { loadScoresWithRetry } from '../api/espn';
import { getUserPicks, doOddsExist, spreadBatch, getNflCurrentWeek } from '../api/league';
import { createSpreadResponse } from '../test/fixtures';

const adapter = createNflAdapter();

function makeScores(homeTeam: string, awayTeam: string, homeScore = 24, awayScore = 17) {
  const comp = createCompetition({ homeTeam, awayTeam, homeScore, awayScore });
  return createScores({ week: 8, events: [{ id: `${homeTeam}vs${awayTeam}`, season: { year: 2023, type: 2 }, week: { number: 8 }, date: new Date().toISOString(), competitions: [comp] }] });
}

describe('nflAdapter', () => {
  beforeEach(() => vi.clearAllMocks());

  describe('loadCurrentGames', () => {
    it('maps ESPN competitions to GameView[]', async () => {
      vi.mocked(loadScoresWithRetry).mockResolvedValue(makeScores('KC', 'BUF'));
      vi.mocked(doOddsExist).mockResolvedValue(true);
      vi.mocked(getUserPicks).mockResolvedValue([]);
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
      vi.mocked(loadScoresWithRetry).mockResolvedValue(makeScores('KC', 'BUF'));
      vi.mocked(doOddsExist).mockResolvedValue(true);
      vi.mocked(getUserPicks).mockResolvedValue([]);
      vi.mocked(spreadBatch).mockResolvedValue({ responses: {
        KC: { ...createSpreadResponse('KC', -3), dateCreated: '2026-01-02T12:00:00Z' },
        BUF: { ...createSpreadResponse('BUF', 3), dateCreated: '2026-01-02T12:00:00Z' },
      }});

      const result = await adapter.loadCurrentGames(1, 'user1');

      expect(result.games[0].spreadPostedAt).toBe('2026-01-02T12:00:00Z');
    });

    it('sets hasOdds=true when odds exist', async () => {
      vi.mocked(loadScoresWithRetry).mockResolvedValue(makeScores('KC', 'BUF'));
      vi.mocked(doOddsExist).mockResolvedValue(true);
      vi.mocked(getUserPicks).mockResolvedValue([]);
      vi.mocked(spreadBatch).mockResolvedValue({ responses: {} });

      const result = await adapter.loadCurrentGames(1, 'user1');
      expect(result.hasOdds).toBe(true);
    });

    it('sets hasOdds=false when no odds', async () => {
      vi.mocked(loadScoresWithRetry).mockResolvedValue(makeScores('KC', 'BUF'));
      vi.mocked(doOddsExist).mockResolvedValue(false);
      vi.mocked(getUserPicks).mockResolvedValue([]);

      const result = await adapter.loadCurrentGames(1, 'user1');
      expect(result.hasOdds).toBe(false);
    });

    it('maps userPicks to PickView[] with gameId matching game.id', async () => {
      const scores = makeScores('KC', 'BUF');
      const gameId = scores.events![0].competitions[0].id;
      vi.mocked(loadScoresWithRetry).mockResolvedValue(scores);
      vi.mocked(doOddsExist).mockResolvedValue(true);
      vi.mocked(getUserPicks).mockResolvedValue([{
        id: 1, leagueId: 1, userId: 'user1', userName: 'Alice',
        team: 'KC', pick: 'Spread' as const, nflWeek: 8, season: 2023, dateCreated: '',
      }]);
      vi.mocked(spreadBatch).mockResolvedValue({ responses: {} });

      const result = await adapter.loadCurrentGames(1, 'user1');
      expect(result.userPicks).toHaveLength(1);
      expect(result.userPicks[0].team).toBe('KC');
      expect(result.userPicks[0].pickType).toBe('Spread');
      expect(result.userPicks[0].gameId).toBe(gameId);
    });
  });

  describe('config', () => {
    it('has pollIntervalMs > 0', () => {
      expect(adapter.pollIntervalMs).toBeGreaterThan(0);
    });
    it('currentSeasonYear returns a number', async () => {
      vi.mocked(loadScoresWithRetry).mockResolvedValue(makeScores('KC', 'BUF'));
      const year = await adapter.currentSeasonYear();
      expect(typeof year).toBe('number');
    });

    // frizat: ESPN's live "current" scoreboard has nothing in progress (off-season, between
    // slates, etc.) — must fall back to NflCurrentWeekService's real resolved season/week via
    // getNflCurrentWeek, not `new Date().getFullYear()` (today's real calendar year, which has
    // no seeded data at all). Uses a fresh adapter instance — getCurrentWeek() caches for the
    // adapter's lifetime, so reusing the shared module-level `adapter` here would leak state
    // into/from other tests in this file.
    it('currentSeasonYear falls back to the resolved current week, not the real calendar year', async () => {
      vi.mocked(loadScoresWithRetry).mockResolvedValue(null);
      vi.mocked(getNflCurrentWeek).mockResolvedValue({
        weekId: 18, espnWeek: 18, season: 2025, isPostSeason: false,
        weekLabel: 'Week 18', scoringFormat: 'Standard', spreadLockDatetime: '2026-01-04T18:00:00Z',
      });

      const freshAdapter = createNflAdapter();
      const year = await freshAdapter.currentSeasonYear();

      expect(year).toBe(2025);
      expect(year).not.toBe(new Date().getFullYear());
    });

    it('loadCurrentGames falls back to the resolved current week when live scores are empty', async () => {
      vi.mocked(loadScoresWithRetry).mockResolvedValue(null);
      vi.mocked(getNflCurrentWeek).mockResolvedValue({
        weekId: 18, espnWeek: 18, season: 2025, isPostSeason: false,
        weekLabel: 'Week 18', scoringFormat: 'Standard', spreadLockDatetime: '2026-01-04T18:00:00Z',
      });

      const freshAdapter = createNflAdapter();
      const result = await freshAdapter.loadCurrentGames(1, 'user1');

      expect(result.season).toBe(2025);
      expect(result.week).toBe(18);
    });
  });
});
