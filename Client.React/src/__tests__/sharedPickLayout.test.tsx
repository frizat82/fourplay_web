/**
 * Regression: both NFL and CFB use the shared PicksPage with GameCard.
 * If either adapter stops returning GameCard-renderable data, these tests fail.
 */
import { render, screen, waitFor } from '@testing-library/react';
import { vi } from 'vitest';
import PicksPage from '../pages/PicksPage';
import { createCfbAdapter } from '../services/cfbAdapter';
import { createNflAdapter } from '../services/nflAdapter';
import type { CfbSlateDto, CfbSpreadDto, CfbScoreDto } from '../types/league';

vi.mock('../services/session', () => ({
  useSession: () => ({
    currentLeague: 1, availableLeagues: [], selectLeague: vi.fn(),
    reloadLeagues: vi.fn(), clearSession: vi.fn(),
    hasNflAccess: true, hasCfbAccess: true, leaguesLoaded: true,
  }),
}));
vi.mock('../services/auth',  () => ({ useAuth: () => ({ user: { userId: 'u1', name: 'Alice', claims: [] } }) }));
vi.mock('../services/toast', () => ({ useToast: () => ({ push: vi.fn() }) }));
vi.mock('../services/sport', () => ({ useSportContext: () => ({ sport: 'NFL', isCfb: false, isNfl: true }) }));
vi.mock('../services/theme', () => ({ useThemeMode: () => ({ mode: 'light', toggleTheme: vi.fn() }) }));
vi.mock('../components/sports/TeamHelmet', () => ({
  default: ({ abbr }: { abbr: string }) => <div data-testid={`helmet-${abbr}`}>{abbr}</div>,
}));
vi.mock('../components/WeatherIcon', () => ({ default: () => null }));

// ─── NFL regression ──────────────────────────────────────────────────────────
vi.mock('../api/espn', () => ({ getScores: vi.fn(), loadScoresWithRetry: vi.fn(), getWeekScores: vi.fn() }));
vi.mock('../api/league', () => ({ addPicks: vi.fn(), doOddsExist: vi.fn(), getUserPicks: vi.fn(), spreadBatch: vi.fn() }));
vi.mock('../api/jersey', () => ({ getAllJerseys: vi.fn() }));
vi.mock('../services/spreadRelease', () => ({ getNextSpreadJob: vi.fn() }));

import { loadScoresWithRetry } from '../api/espn';
import { doOddsExist, getUserPicks, spreadBatch } from '../api/league';
import { getAllJerseys } from '../api/jersey';
import { getNextSpreadJob } from '../services/spreadRelease';
import { createCompetition, createScores, createSpreadResponse } from '../test/fixtures';

describe('NFL PicksPage — GameCard layout regression', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    const scores = createScores({ week: 8, events: [{
      id: '1', season: { year: 2023, type: 2 }, week: { number: 8 }, date: new Date().toISOString(),
      competitions: [createCompetition({ homeTeam: 'KC', awayTeam: 'BUF' })],
    }]});
    vi.mocked(loadScoresWithRetry).mockResolvedValue(scores);
    vi.mocked(doOddsExist).mockResolvedValue(true);
    vi.mocked(getUserPicks).mockResolvedValue([]);
    vi.mocked(spreadBatch).mockResolvedValue({ responses: {
      KC: createSpreadResponse('KC', -3),
      BUF: createSpreadResponse('BUF', 3),
    }});
    vi.mocked(getAllJerseys).mockResolvedValue({});
    vi.mocked(getNextSpreadJob).mockResolvedValue(null);
  });

  it('renders Pick buttons (GameCard pick mode)', async () => {
    render(<PicksPage adapter={createNflAdapter()} />);
    await waitFor(() => expect(screen.getAllByRole('button', { name: /^pick$/i }).length).toBeGreaterThan(0));
  });

  it('renders spread label -3 (GameCard spread display)', async () => {
    render(<PicksPage adapter={createNflAdapter()} />);
    await waitFor(() => expect(screen.getByText('-3')).toBeInTheDocument());
  });
});

// ─── CFB regression ──────────────────────────────────────────────────────────
vi.mock('../api/cfb', () => ({
  getCfbSlates: vi.fn(), getCfbSpreads: vi.fn(), getCfbScores: vi.fn(),
  getCfbUserPicks: vi.fn(), addCfbPicks: vi.fn(), deleteCfbPicks: vi.fn(),
}));

import { getCfbSlates, getCfbSpreads, getCfbScores, getCfbUserPicks } from '../api/cfb';

const slate: CfbSlateDto = { id: 1, season: 2025, slateNumber: 8, label: 'Week 8', slateType: 'RegularSeason', startDate: '2025-10-11', endDate: '2025-10-18' };
const spread: CfbSpreadDto = { id: 1, cfbSlateId: 1, espnEventId: 100, homeTeam: 'MICH', awayTeam: 'PSU', homeTeamSpread: -3.5, awayTeamSpread: 3.5, overUnder: 44.5, gameTime: '2030-10-11T20:00:00Z' };
const cfbScore: CfbScoreDto = { id: 1, cfbSlateId: 1, espnEventId: 100, homeTeam: 'MICH', awayTeam: 'PSU', homeTeamScore: 0, awayTeamScore: 0, gameStatus: 'StatusScheduled', gameTime: '2030-10-11T20:00:00Z' };

describe('CFB PicksPage (via adapter) — GameCard layout regression', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(getCfbSlates).mockResolvedValue([slate]);
    vi.mocked(getCfbSpreads).mockResolvedValue([spread]);
    vi.mocked(getCfbScores).mockResolvedValue([cfbScore]);
    vi.mocked(getCfbUserPicks).mockResolvedValue([]);
  });

  it('renders Pick buttons (GameCard pick mode)', async () => {
    render(<PicksPage adapter={createCfbAdapter()} />);
    await waitFor(() => expect(screen.getAllByRole('button', { name: /^pick$/i }).length).toBeGreaterThan(0));
  });

  it('renders spread labels -3.5 and +3.5 (GameCard spread display)', async () => {
    render(<PicksPage adapter={createCfbAdapter()} />);
    await waitFor(() => {
      expect(screen.getByText('-3.5')).toBeInTheDocument();
      expect(screen.getByText('+3.5')).toBeInTheDocument();
    });
  });

  it('renders TeamHelmet for both teams', async () => {
    render(<PicksPage adapter={createCfbAdapter()} />);
    await waitFor(() => {
      expect(screen.getByTestId('helmet-MICH')).toBeInTheDocument();
      expect(screen.getByTestId('helmet-PSU')).toBeInTheDocument();
    });
  });

  it('shows Picked when pick already submitted', async () => {
    vi.mocked(getCfbUserPicks).mockResolvedValue([
      { id: 1, userId: 'u1', leagueId: 1, cfbSlateId: 1, espnEventId: 100, team: 'MICH', pickType: 'Spread', season: 2025 }
    ]);
    render(<PicksPage adapter={createCfbAdapter()} />);
    await waitFor(() => expect(screen.getByRole('button', { name: /^picked$/i })).toBeInTheDocument());
  });
});
