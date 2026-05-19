import { vi } from 'vitest';
import { createCfbAdapter } from '../services/cfbAdapter';
import type { CfbSlateDto, CfbSpreadDto, CfbScoreDto, CfbPickDto } from '../types/league';

vi.mock('../api/cfb', () => ({
  getCfbSlates: vi.fn(),
  getCfbSpreads: vi.fn(),
  getCfbScores: vi.fn(),
  getCfbUserPicks: vi.fn(),
  addCfbPicks: vi.fn(),
  deleteCfbPicks: vi.fn(),
}));

import { getCfbSlates, getCfbSpreads, getCfbScores, getCfbUserPicks } from '../api/cfb';

const slate: CfbSlateDto = {
  id: 10, season: 2025, slateNumber: 8, label: 'Week 8',
  slateType: 'RegularSeason', startDate: '2025-10-11', endDate: '2025-10-18',
};
const spread: CfbSpreadDto = {
  id: 1, cfbSlateId: 10, espnEventId: 999, homeTeam: 'MICH', awayTeam: 'PSU',
  homeTeamSpread: -3.5, awayTeamSpread: 3.5, overUnder: 44.5,
  gameTime: '2025-10-11T20:00:00Z',
};
const score: CfbScoreDto = {
  id: 1, cfbSlateId: 10, espnEventId: 999, homeTeam: 'MICH', awayTeam: 'PSU',
  homeTeamScore: 27, awayTeamScore: 13, gameStatus: 'StatusFinal',
  gameTime: '2025-10-11T20:00:00Z',
};

const adapter = createCfbAdapter();

describe('cfbAdapter', () => {
  beforeEach(() => vi.clearAllMocks());

  describe('loadCurrentGames', () => {
    it('maps CfbSpreadDto + CfbScoreDto to GameView[]', async () => {
      vi.mocked(getCfbSlates).mockResolvedValue([slate]);
      vi.mocked(getCfbSpreads).mockResolvedValue([spread]);
      vi.mocked(getCfbScores).mockResolvedValue([score]);
      vi.mocked(getCfbUserPicks).mockResolvedValue([]);

      const result = await adapter.loadCurrentGames(1, 'user1');

      expect(result.games).toHaveLength(1);
      const game = result.games[0];
      expect(game.homeTeam).toBe('MICH');
      expect(game.awayTeam).toBe('PSU');
      expect(game.homeSpread).toBe(-3.5);
      expect(game.awaySpread).toBe(3.5);
      expect(game.overUnder).toBe(44.5);
      expect(game.homeScore).toBe(27);
      expect(game.awayScore).toBe(13);
      expect(game.gameStatus).toBe('StatusFinal');
      expect(game.id).toBe('999');
    });

    it('always sets hasOdds=true when spreads exist', async () => {
      vi.mocked(getCfbSlates).mockResolvedValue([slate]);
      vi.mocked(getCfbSpreads).mockResolvedValue([spread]);
      vi.mocked(getCfbScores).mockResolvedValue([]);
      vi.mocked(getCfbUserPicks).mockResolvedValue([]);

      const result = await adapter.loadCurrentGames(1, 'user1');
      expect(result.hasOdds).toBe(true);
    });

    it('sets hasOdds=false when no spreads exist', async () => {
      vi.mocked(getCfbSlates).mockResolvedValue([slate]);
      vi.mocked(getCfbSpreads).mockResolvedValue([]);
      vi.mocked(getCfbScores).mockResolvedValue([]);
      vi.mocked(getCfbUserPicks).mockResolvedValue([]);

      const result = await adapter.loadCurrentGames(1, 'user1');
      expect(result.hasOdds).toBe(false);
    });

    it('maps CfbPickDto to PickView with stringified gameId', async () => {
      const pick: CfbPickDto = {
        id: 1, userId: 'user1', leagueId: 1, cfbSlateId: 10,
        espnEventId: 999, team: 'MICH', pickType: 'Spread', season: 2025,
      };
      vi.mocked(getCfbSlates).mockResolvedValue([slate]);
      vi.mocked(getCfbSpreads).mockResolvedValue([spread]);
      vi.mocked(getCfbScores).mockResolvedValue([score]);
      vi.mocked(getCfbUserPicks).mockResolvedValue([pick]);

      const result = await adapter.loadCurrentGames(1, 'user1');
      expect(result.userPicks).toHaveLength(1);
      expect(result.userPicks[0].gameId).toBe('999');
      expect(result.userPicks[0].team).toBe('MICH');
      expect(result.userPicks[0].pickType).toBe('Spread');
    });

    it('derives WeekState from slate slateNumber', async () => {
      vi.mocked(getCfbSlates).mockResolvedValue([slate]); // slateNumber=8
      vi.mocked(getCfbSpreads).mockResolvedValue([]);
      vi.mocked(getCfbScores).mockResolvedValue([]);
      vi.mocked(getCfbUserPicks).mockResolvedValue([]);

      const result = await adapter.loadCurrentGames(1, 'user1');
      expect(result.week).toBe(8);
      expect(result.isPostSeason).toBe(false);
      expect(result.season).toBe(2025);
    });
  });

  describe('config', () => {
    it('pollIntervalMs is 0 (no polling)', () => {
      expect(adapter.pollIntervalMs).toBe(0);
    });
    it('supportsJerseys is false', () => {
      expect(adapter.supportsJerseys).toBe(false);
    });
    it('currentSeasonYear returns 2025', async () => {
      expect(await adapter.currentSeasonYear()).toBe(2025);
    });
    it('weekLabelFn returns CFP Championship for week 5 postseason', () => {
      const fn = adapter.weekSelectorConfig.weekLabelFn!;
      expect(fn(5, true)).toBe('CFP Championship');
      expect(() => fn(5, true)).not.toThrow();
    });
  });
});
