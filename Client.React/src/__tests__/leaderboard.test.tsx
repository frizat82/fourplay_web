import { render, screen } from '@testing-library/react';
import LeaderboardPage from '../pages/LeaderboardPage';
import { vi } from 'vitest';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { createLeaderboardEntry, createLeaderboardWeekResult } from '../test/fixtures';
import type { SportAdapter } from '../services/sportAdapter';

const sessionState = {
  currentLeague: 1 as number | null,
  availableLeagues: [],
  selectLeague: vi.fn(),
  reloadLeagues: vi.fn(),
  clearSession: vi.fn(),
  hasNflAccess: true,
  hasCfbAccess: false,
  leaguesLoaded: true,
};

vi.mock('../services/session', () => ({ useSession: () => sessionState }));
vi.mock('../services/auth', () => ({ useAuth: () => ({ user: { userId: '123', name: 'TestUser', claims: [] } }) }));
vi.mock('../api/leaderboard', () => ({ getLeaderboard: vi.fn() }));

import { getLeaderboard } from '../api/leaderboard';

const mockedGetLeaderboard = vi.mocked(getLeaderboard);

const mockAdapter: SportAdapter = {
  loadCurrentGames: vi.fn(),
  loadHistoricalGames: vi.fn(),
  loadCurrentScores: vi.fn(),
  loadHistoricalScores: vi.fn(),
  submitPicks: vi.fn(),
  clearPicks: vi.fn(),
  currentSeasonYear: vi.fn().mockResolvedValue(2023),
  pollIntervalMs: 0,
  weekSelectorConfig: { maxRegularSeasonWeek: 18, minSeason: 2020 },
};

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/leaderboard']}>
      <Routes>
        <Route path="/leaderboard" element={<LeaderboardPage adapter={mockAdapter} />} />
        <Route path="/leaguepicker" element={<div>League Picker</div>} />
      </Routes>
    </MemoryRouter>
  );
}

describe('LeaderboardPage', () => {
  beforeEach(() => {
    sessionState.currentLeague = 1;
    mockedGetLeaderboard.mockReset();
    vi.mocked(mockAdapter.currentSeasonYear).mockResolvedValue(2023);
  });

  it('renders leaderboard with valid data', async () => {
    mockedGetLeaderboard.mockResolvedValue([
      createLeaderboardEntry({
        userId: '123', userName: 'TestUser', rank: '1', total: 5,
        weekResults: [
          createLeaderboardWeekResult({ week: 1, score: 25, weekResult: 'Won' }),
          createLeaderboardWeekResult({ week: 2, score: 20, weekResult: 'Lost' }),
        ],
      }),
    ]);

    renderPage();
    await screen.findByText(/TestUser/i);
    expect(screen.getByText(/TestUser/i)).toBeInTheDocument();
    expect(screen.getByText('25')).toBeInTheDocument();
  });

  it('renders without crash when users have ragged weekResults', async () => {
    mockedGetLeaderboard.mockResolvedValue([
      createLeaderboardEntry({
        userId: '123', userName: 'TestUser', rank: '1', total: 5,
        weekResults: [
          createLeaderboardWeekResult({ week: 1, score: 25, weekResult: 'Won' }),
          createLeaderboardWeekResult({ week: 2, score: 20, weekResult: 'Won' }),
          createLeaderboardWeekResult({ week: 3, score: 15, weekResult: 'Won' }),
        ],
      }),
      createLeaderboardEntry({ userId: '456', userName: 'NewUser', rank: '2', total: 0, weekResults: [] }),
    ]);

    renderPage();
    await screen.findByText(/TestUser/i);
    expect(screen.getByText(/NewUser/i)).toBeInTheDocument();
  });

  it('shows no league message when no league selected', async () => {
    sessionState.currentLeague = null;
    renderPage();
    await screen.findByText(/League Picker/i);
  });
});
