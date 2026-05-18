/**
 * Regression tests: CfbPicksPage must render using the shared GameCard layout
 * (same Pick/Picked buttons and spread labels as PicksPage).
 *
 * The NFL picks.test.tsx already guards the NFL side. This file guards CFB.
 * If CfbPicksPage ever reverts to custom inline rendering instead of GameCard,
 * these tests will fail because GameCard is what produces the 'Pick' buttons
 * and numeric spread labels.
 */
import { render, screen, waitFor } from '@testing-library/react';
import { vi } from 'vitest';
import CfbPicksPage from '../pages/CfbPicksPage';
import type { CfbSlateDto, CfbSpreadDto, CfbScoreDto } from '../types/league';

vi.mock('../services/session', () => ({
  useSession: () => ({
    currentLeague: 1,
    availableLeagues: [],
    selectLeague: vi.fn(),
    reloadLeagues: vi.fn(),
    clearSession: vi.fn(),
    hasNflAccess: true,
    hasCfbAccess: true,
    leaguesLoaded: true,
  }),
}));
vi.mock('../services/auth',  () => ({ useAuth: () => ({ user: { userId: 'u1', name: 'Alice', claims: [] } }) }));
vi.mock('../services/toast', () => ({ useToast: () => ({ push: vi.fn() }) }));
vi.mock('../components/sports/TeamHelmet', () => ({
  default: ({ abbr }: { abbr: string }) => <div data-testid={`helmet-${abbr}`}>{abbr}</div>,
}));
vi.mock('../components/WeatherIcon', () => ({ default: () => null }));
vi.mock('../api/cfb', () => ({
  getCfbSlates:    vi.fn(),
  getCfbSpreads:   vi.fn(),
  getCfbScores:    vi.fn(),
  getCfbUserPicks: vi.fn(),
  addCfbPicks:     vi.fn(),
  deleteCfbPicks:  vi.fn(),
}));

import { getCfbSlates, getCfbSpreads, getCfbScores, getCfbUserPicks } from '../api/cfb';

const slate: CfbSlateDto = {
  id: 1, season: 2025, slateNumber: 8, label: 'Week 8',
  slateType: 'RegularSeason', startDate: '2025-10-11', endDate: '2025-10-18',
};
const spread: CfbSpreadDto = {
  id: 1, cfbSlateId: 1, espnEventId: 100, homeTeam: 'MICH', awayTeam: 'PSU',
  homeTeamSpread: -3.5, awayTeamSpread: 3.5, overUnder: 44.5,
  gameTime: '2030-10-11T20:00:00Z', // far future = not locked
};
const score: CfbScoreDto = {
  id: 1, cfbSlateId: 1, espnEventId: 100, homeTeam: 'MICH', awayTeam: 'PSU',
  homeTeamScore: 0, awayTeamScore: 0, gameStatus: 'StatusScheduled',
  gameTime: '2030-10-11T20:00:00Z',
};

describe('CfbPicksPage — GameCard layout regression', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(getCfbSlates).mockResolvedValue([slate]);
    vi.mocked(getCfbSpreads).mockResolvedValue([spread]);
    vi.mocked(getCfbScores).mockResolvedValue([score]);
    vi.mocked(getCfbUserPicks).mockResolvedValue([]);
  });

  it('renders Pick buttons (GameCard pick mode — not inline buttons)', async () => {
    render(<CfbPicksPage />);
    await waitFor(() =>
      expect(screen.getAllByRole('button', { name: /^pick$/i }).length).toBeGreaterThan(0)
    );
  });

  it('renders spread labels as formatted numbers (GameCard spreadLabel output)', async () => {
    render(<CfbPicksPage />);
    await waitFor(() => {
      expect(screen.getByText('-3.5')).toBeInTheDocument();
      expect(screen.getByText('+3.5')).toBeInTheDocument();
    });
  });

  it('renders TeamHelmet for both teams (GameCard logo rendering)', async () => {
    render(<CfbPicksPage />);
    await waitFor(() => {
      expect(screen.getByTestId('helmet-MICH')).toBeInTheDocument();
      expect(screen.getByTestId('helmet-PSU')).toBeInTheDocument();
    });
  });

  it('shows Picked button when a pick is already submitted', async () => {
    vi.mocked(getCfbUserPicks).mockResolvedValue([
      { id: 1, userId: 'u1', leagueId: 1, cfbSlateId: 1, espnEventId: 100, team: 'MICH', pickType: 'Spread', season: 2025 }
    ]);
    render(<CfbPicksPage />);
    await waitFor(() => expect(screen.getByRole('button', { name: /^picked$/i })).toBeInTheDocument());
  });
});
