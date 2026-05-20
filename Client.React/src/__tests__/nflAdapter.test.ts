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
}));
vi.mock('../api/jersey', () => ({ getAllJerseys: vi.fn() }));

import { loadScoresWithRetry } from '../api/espn';
import { getUserPicks, doOddsExist, spreadBatch } from '../api/league';
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
    it('supportsJerseys is true', () => {
      expect(adapter.supportsJerseys).toBe(true);
    });
    it('currentSeasonYear returns a number', async () => {
      vi.mocked(loadScoresWithRetry).mockResolvedValue(makeScores('KC', 'BUF'));
      const year = await adapter.currentSeasonYear();
      expect(typeof year).toBe('number');
    });
  });
});
