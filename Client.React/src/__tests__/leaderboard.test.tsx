import { render, screen } from '@testing-library/react';
import LeaderboardPage from '../pages/LeaderboardPage';
import { vi } from 'vitest';
import { MemoryRouter, Route, Routes } from 'react-router-dom';

const sessionState = {
  currentLeague: 1 as number | null,
  availableLeagues: [],
  selectLeague: vi.fn(),
  reloadLeagues: vi.fn(),
  clearSession: vi.fn(),
};

const authState = {
  user: { userId: '123', name: 'TestUser', claims: [] },
};

vi.mock('../services/session', () => ({ useSession: () => sessionState }));
vi.mock('../services/auth', () => ({ useAuth: () => authState }));

vi.mock('../api/espn', () => ({ getScores: vi.fn() }));
vi.mock('../api/leaderboard', () => ({ getLeaderboard: vi.fn() }));

import { getScores } from '../api/espn';
import { getLeaderboard } from '../api/leaderboard';

const mockedGetScores = vi.mocked(getScores);
const mockedGetLeaderboard = vi.mocked(getLeaderboard);

describe('LeaderboardPage', () => {
  beforeEach(() => {
    sessionState.currentLeague = 1;
    mockedGetScores.mockReset();
    mockedGetLeaderboard.mockReset();
  });

  it('renders leaderboard with valid data', async () => {
    mockedGetScores.mockResolvedValue({
      season: { year: 2025, type: 2 },
      week: { number: 2 },
      events: [{ id: '1', season: { year: 2025, type: 2 }, week: { number: 2 }, date: new Date().toISOString(), competitions: [] }],
    });

    mockedGetLeaderboard.mockResolvedValue([
      {
        userId: '123',
        userName: 'TestUser',
        rank: '1',
        total: 5,
        weekResults: [
          { week: 1, score: 25, weekResult: 'Won' },
          { week: 2, score: 20, weekResult: 'Lost' },
        ],
      },
    ]);

    render(
      <MemoryRouter initialEntries={['/leaderboard']}>
        <Routes>
          <Route path="/leaderboard" element={<LeaderboardPage />} />
          <Route path="/leaguepicker" element={<div>League Picker</div>} />
        </Routes>
      </MemoryRouter>
    );

    await screen.findByText(/TestUser/i);
    expect(screen.getByText(/TestUser/i)).toBeInTheDocument();
    expect(screen.getByText('25')).toBeInTheDocument();
  });

  it('shows no league message when no league selected', async () => {
    sessionState.currentLeague = null;
    mockedGetScores.mockResolvedValue({
      season: { year: 2025, type: 2 },
      week: { number: 2 },
      events: [],
    });

    render(
      <MemoryRouter initialEntries={['/leaderboard']}>
        <Routes>
          <Route path="/leaderboard" element={<LeaderboardPage />} />
          <Route path="/leaguepicker" element={<div>League Picker</div>} />
        </Routes>
      </MemoryRouter>
    );
    await screen.findByText(/League Picker/i);
  });
});
