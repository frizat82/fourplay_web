import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { alpha, ThemeProvider } from '@mui/material';
import LeaderboardPage from '../pages/LeaderboardPage';
import { vi } from 'vitest';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { createLeaderboardEntry, createLeaderboardWeekResult } from '../test/fixtures';
import type { SportAdapter } from '../services/sportAdapter';
import { createAppTheme } from '../app/theme';

const toastPush = vi.fn();
vi.mock('../services/toast', () => ({ useToast: () => ({ push: toastPush }) }));

const mockedToBlob = vi.fn();
vi.mock('html-to-image', () => ({ toBlob: (...args: unknown[]) => mockedToBlob(...args) }));

const sessionState = {
  currentLeague: 1 as number | null,
  availableLeagues: [{ leagueId: 1, leagueName: 'Demo League' }] as { leagueId: number; leagueName: string }[],
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
  sport: 'nfl',
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

function renderPage(mode: 'light' | 'dark' = 'light') {
  return render(
    <ThemeProvider theme={createAppTheme(mode)}>
      <MemoryRouter initialEntries={['/leaderboard']}>
        <Routes>
          <Route path="/leaderboard" element={<LeaderboardPage adapter={mockAdapter} />} />
          <Route path="/leaguepicker" element={<div>League Picker</div>} />
        </Routes>
      </MemoryRouter>
    </ThemeProvider>
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
    const table = await screen.findByRole('table');
    expect(within(table).getByText('TestUser')).toBeInTheDocument();
    expect(screen.getByText('25')).toBeInTheDocument();
  });

  it('Share button shares a rendered standings-card image, not a link', async () => {
    const shareMock = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'canShare', { value: () => true, configurable: true });
    Object.defineProperty(navigator, 'share', { value: shareMock, configurable: true });
    mockedToBlob.mockResolvedValue(new Blob(['fake-png'], { type: 'image/png' }));
    mockedGetLeaderboard.mockResolvedValue([
      createLeaderboardEntry({ userId: '123', userName: 'TestUser', rank: '1', total: 5, weekResults: [] }),
    ]);

    renderPage();
    await screen.findByRole('table');
    await userEvent.click(screen.getByRole('button', { name: /^share$/i }));

    await waitFor(() => expect(shareMock).toHaveBeenCalled());
    const call = shareMock.mock.calls[0][0];
    expect(call.files).toHaveLength(1);
    expect(call.files[0].type).toBe('image/png');
    expect(call.url).toBeUndefined();
  });

  it('Share button falls back to link-share when the viewer is not on the leaderboard', async () => {
    const shareMock = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'canShare', { value: () => true, configurable: true });
    Object.defineProperty(navigator, 'share', { value: shareMock, configurable: true });
    mockedGetLeaderboard.mockResolvedValue([
      createLeaderboardEntry({ userId: '999', userName: 'SomeoneElse', rank: '1', total: 5, weekResults: [] }),
    ]);

    renderPage();
    await screen.findByText(/SomeoneElse/i);
    await userEvent.click(screen.getByRole('button', { name: /^share$/i }));

    await waitFor(() => expect(shareMock).toHaveBeenCalledWith(
      expect.objectContaining({ title: expect.stringMatching(/standings/i), url: window.location.href })
    ));
    expect(mockedToBlob).not.toHaveBeenCalled();
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
    await screen.findByRole('table');
    expect(screen.getByText(/NewUser/i)).toBeInTheDocument();
  });

  it('shows no league message when no league selected', async () => {
    sessionState.currentLeague = null;
    renderPage();
    await screen.findByText(/League Picker/i);
  });
});

describe('LeaderboardPage — week-cell colors', () => {
  beforeEach(() => {
    sessionState.currentLeague = 1;
    mockedGetLeaderboard.mockReset();
    vi.mocked(mockAdapter.currentSeasonYear).mockResolvedValue(2023);
  });

  // frizat: getWeekSx used to hardcode literal rgba() constants (e.g. rgba(22, 163, 74, 0.12) for
  // "Won") that don't match theme.ts's actual success/error/warning/info hex values at all, and
  // stayed fixed at the same opacity in both light and dark mode — reported as hard to read.
  // Deriving the background via alpha(theme.palette.X.main, ...) guarantees it always matches the
  // theme's own already-mode-tuned color, in both themes.
  it.each([
    ['light', 'Won', 'success'] as const,
    ['dark', 'Won', 'success'] as const,
    ['light', 'MissingPicks', 'warning'] as const,
    ['dark', 'MissingPicks', 'warning'] as const,
    ['light', 'MissingGameResults', 'info'] as const,
    ['dark', 'MissingGameResults', 'info'] as const,
    ['light', 'Lost', 'error'] as const,
    ['dark', 'Lost', 'error'] as const,
  ])('%s mode: %s cell background is derived from theme.palette.%s.main', async (mode, weekResult, paletteKey) => {
    mockedGetLeaderboard.mockResolvedValue([
      createLeaderboardEntry({
        userId: '123', userName: 'TestUser', rank: '1', total: 5,
        weekResults: [createLeaderboardWeekResult({ week: 1, score: 25, weekResult })],
      }),
    ]);

    renderPage(mode);
    const cell = (await screen.findByText('25')).closest('td');
    const theme = createAppTheme(mode);
    const expected = alpha(theme.palette[paletteKey].main, 0.16);
    expect(cell).toHaveStyle({ backgroundColor: expected });
  });
});
