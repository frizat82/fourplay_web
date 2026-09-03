import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { alpha, ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import LeaderboardPage from '../pages/LeaderboardPage';
import { vi } from 'vitest';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { createLeaderboardEntry, createLeaderboardWeekResult, mockLeagueJuiceEmpty } from '../test/fixtures';
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
vi.mock('../api/league', () => ({ getLeagueJuiceForSeason: vi.fn(), getLeagueJuice: vi.fn() }));

import { getLeaderboard } from '../api/leaderboard';
import { getLeagueJuiceForSeason, getLeagueJuice } from '../api/league';

const mockedGetLeaderboard = vi.mocked(getLeaderboard);
const mockedGetLeagueJuiceForSeason = vi.mocked(getLeagueJuiceForSeason);
const mockedGetLeagueJuice = vi.mocked(getLeagueJuice);

// frizat-ugs: every existing test in this file exercises a season whose Juice IS configured —
// default the mock to "configured" globally so only the new not-configured tests need to
// override it, instead of touching all the pre-existing test bodies below.
beforeEach(() => {
  mockedGetLeagueJuiceForSeason.mockReset();
  mockedGetLeagueJuiceForSeason.mockResolvedValue({
    id: 1, leagueId: 1, leagueName: 'Demo League', season: 2023,
    juice: 13, juiceDivisional: 10, juiceConference: 6, weeklyCost: 5, dateCreated: '2023-01-01T00:00:00Z',
  });
  mockLeagueJuiceEmpty(mockedGetLeagueJuice);
});

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
  // useLeagueMinSeason (via LeaderboardPage) now goes through React Query — a fresh client per
  // render so cached ['leagueJuice', leagueId] results never leak between tests.
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return {
    ...render(
      <QueryClientProvider client={queryClient}>
        <ThemeProvider theme={createAppTheme(mode)}>
          <MemoryRouter initialEntries={['/leaderboard']}>
            <Routes>
              <Route path="/leaderboard" element={<LeaderboardPage adapter={mockAdapter} />} />
              <Route path="/leaguepicker" element={<div>League Picker</div>} />
            </Routes>
          </MemoryRouter>
        </ThemeProvider>
      </QueryClientProvider>
    ),
    queryClient,
  };
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

  // frizat-ugs: see the juiceConfigured state comment in LeaderboardPage.tsx for the full context.
  it('shows a "Juice not configured" message instead of the generic empty state when Juice is unconfigured', async () => {
    mockedGetLeagueJuiceForSeason.mockResolvedValue(null);
    mockedGetLeaderboard.mockResolvedValue([]);

    renderPage();
    await screen.findByText(/juice.*not configured/i);
    expect(screen.queryByText(/no leaderboard data yet/i)).not.toBeInTheDocument();
  });

  it('keeps the generic "no data yet" message when Juice IS configured but the leaderboard is still empty', async () => {
    mockedGetLeaderboard.mockResolvedValue([]);

    renderPage();
    await screen.findByText(/no leaderboard data yet/i);
    expect(screen.queryByText(/juice.*not configured/i)).not.toBeInTheDocument();
  });

  // /code-review: the juice-configured lookup is a second await inside the same fetch effect —
  // without a try/finally around it, a rejected request there (network error, non-401 failure;
  // http.ts's interceptor only retries on 401) left the page stuck on the spinner forever, since
  // nothing after the throw ever called setLoading(false).
  it('still clears the loading spinner when the follow-up Juice-configured check fails', async () => {
    mockedGetLeaderboard.mockResolvedValue([]);
    mockedGetLeagueJuiceForSeason.mockRejectedValue(new Error('network down'));

    renderPage();
    await waitFor(() => expect(screen.queryByRole('progressbar')).not.toBeInTheDocument());
  });
});

describe('LeaderboardPage — season selector', () => {
  beforeEach(() => {
    sessionState.currentLeague = 1;
    mockedGetLeaderboard.mockReset();
    mockedGetLeaderboard.mockResolvedValue([
      createLeaderboardEntry({ userId: '123', userName: 'TestUser', rank: '1', total: 5, weekResults: [] }),
    ]);
    vi.mocked(mockAdapter.currentSeasonYear).mockResolvedValue(2023);
  });

  it('defaults to the current season and loads it on mount', async () => {
    renderPage();
    await screen.findByRole('table');
    expect(screen.getByText('2023 Season')).toBeInTheDocument();
    expect(mockedGetLeaderboard).toHaveBeenCalledWith(1, 2023);
  });

  it('lets the viewer pick a previous season, like ESPN — re-fetches standings for that year', async () => {
    renderPage();
    await screen.findByRole('table');
    mockedGetLeaderboard.mockClear();

    await userEvent.click(screen.getByRole('combobox'));
    await userEvent.click(screen.getByRole('option', { name: '2021 Season' }));

    await waitFor(() => expect(mockedGetLeaderboard).toHaveBeenCalledWith(1, 2021));
    expect(screen.getByText('2021 Season')).toBeInTheDocument();
  });

  it('offers every season from adapter.weekSelectorConfig.minSeason through the current season, when the league has no juice history', async () => {
    renderPage();
    await screen.findByRole('table');

    await userEvent.click(screen.getByRole('combobox'));
    expect(screen.getByRole('option', { name: '2023 Season' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: '2020 Season' })).toBeInTheDocument(); // mockAdapter.weekSelectorConfig.minSeason
    expect(screen.queryByRole('option', { name: '2019 Season' })).not.toBeInTheDocument();
    expect(screen.queryByRole('option', { name: '2024 Season' })).not.toBeInTheDocument();
  });

  it('floors the season range at the league\'s own earliest juice-mapping season, not the sport-wide default', async () => {
    // frizat nitpick: a brand-new league (e.g. "OG FourPlayaz" on NFL, "CFB Beta Testers" on CFB)
    // showed years before it ever existed, because the selector's floor was a hardcoded per-sport
    // constant instead of that league's own history. One shared hook (useLeagueMinSeason) fixes
    // both sports identically.
    mockedGetLeagueJuice.mockResolvedValue([
      { id: 1, leagueId: 1, leagueName: 'Demo League', season: 2022, juice: 13, juiceDivisional: 10, juiceConference: 6, weeklyCost: 5, dateCreated: '2022-01-01T00:00:00Z' },
      { id: 2, leagueId: 1, leagueName: 'Demo League', season: 2023, juice: 13, juiceDivisional: 10, juiceConference: 6, weeklyCost: 5, dateCreated: '2023-01-01T00:00:00Z' },
    ]);

    renderPage();
    await screen.findByRole('table');

    await userEvent.click(screen.getByRole('combobox'));
    expect(screen.getByRole('option', { name: '2023 Season' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: '2022 Season' })).toBeInTheDocument();
    expect(screen.queryByRole('option', { name: '2021 Season' })).not.toBeInTheDocument();
    expect(screen.queryByRole('option', { name: '2020 Season' })).not.toBeInTheDocument(); // sport-wide default, below the league's own floor
  });

  it('disables the season selector while a season change is loading, preventing overlapping requests', async () => {
    // frizat: /code-review flagged that rapidly re-selecting seasons had no race guard — a
    // slower earlier request resolving after a later one could leave the table showing the
    // wrong season's data. Rather than layering a manual staleness-check on top, disable the
    // control while a fetch is in flight so a second request can never be fired before the
    // first resolves — simpler, and standard behavior for a data-driven selector mid-fetch.
    let resolveFetch!: (value: ReturnType<typeof createLeaderboardEntry>[]) => void;
    mockedGetLeaderboard.mockImplementationOnce(() => Promise.resolve([
      createLeaderboardEntry({ userId: '123', userName: 'TestUser', rank: '1', total: 5, weekResults: [] }),
    ]));

    renderPage();
    await screen.findByRole('table');

    mockedGetLeaderboard.mockImplementationOnce(() => new Promise((res) => { resolveFetch = res; }));
    await userEvent.click(screen.getByRole('combobox'));
    await userEvent.click(screen.getByRole('option', { name: '2021 Season' }));

    expect(screen.getByRole('combobox')).toHaveAttribute('aria-disabled', 'true');

    resolveFetch([createLeaderboardEntry({ userId: '456', userName: 'PastYearUser', rank: '1', total: 2, weekResults: [] })]);
    await screen.findByText('PastYearUser');
    expect(screen.getByRole('combobox')).not.toHaveAttribute('aria-disabled', 'true');
  });

  it('shows the full loading state (not stale data) when the current league changes, not just the season', async () => {
    // frizat: /code-review caught that the "only show a light refresh, not a full spinner, after
    // the first load" optimization was keyed on ANY fetch ever completing, not on the currently
    // selected league — switching leagues via AppLayout's league-selector chip while sitting on
    // this page (a real, reachable flow, unrelated to this page's own season selector) left the
    // PREVIOUS league's standings rendered, unlabeled as stale, while the new league's data
    // loaded, with no guard against an out-of-order response overwriting the right one.
    mockedGetLeaderboard.mockResolvedValueOnce([
      createLeaderboardEntry({ userId: '123', userName: 'LeagueOneUser', rank: '1', total: 5, weekResults: [] }),
    ]);
    const { rerender, queryClient } = renderPage();
    await screen.findByText('LeagueOneUser');

    let resolveLeagueTwo!: (value: ReturnType<typeof createLeaderboardEntry>[]) => void;
    mockedGetLeaderboard.mockImplementationOnce(() => new Promise((res) => { resolveLeagueTwo = res; }));

    sessionState.currentLeague = 2;
    rerender(
      <QueryClientProvider client={queryClient}>
        <ThemeProvider theme={createAppTheme('light')}>
          <MemoryRouter initialEntries={['/leaderboard']}>
            <Routes>
              <Route path="/leaderboard" element={<LeaderboardPage adapter={mockAdapter} />} />
              <Route path="/leaguepicker" element={<div>League Picker</div>} />
            </Routes>
          </MemoryRouter>
        </ThemeProvider>
      </QueryClientProvider>,
    );

    // The old league's data must not still be on screen while the new league loads.
    await waitFor(() => expect(screen.queryByText('LeagueOneUser')).not.toBeInTheDocument());

    resolveLeagueTwo([createLeaderboardEntry({ userId: '789', userName: 'LeagueTwoUser', rank: '1', total: 9, weekResults: [] })]);
    await screen.findByText('LeagueTwoUser');
  });

  // /code-review: the season selector's own race is prevented by disabling it mid-fetch (see the
  // test above), but AppLayout's separate league-switcher chip lives outside this page — nothing
  // stops a user from switching league A -> B -> A again while earlier fetches are still in
  // flight, and the fetch effect had no staleness guard: whichever response happened to resolve
  // last was applied, regardless of whether it matched the currently-selected league.
  it('ignores a stale response from an abandoned league switch, even if it resolves after a newer one', async () => {
    mockedGetLeaderboard.mockResolvedValueOnce([
      createLeaderboardEntry({ userId: '1', userName: 'LeagueOneUser', rank: '1', total: 1, weekResults: [] }),
    ]);
    const { rerender, queryClient } = renderPage();
    await screen.findByText('LeagueOneUser');

    const rerenderWithLeague = (leagueId: number) => {
      sessionState.currentLeague = leagueId;
      rerender(
        <QueryClientProvider client={queryClient}>
          <ThemeProvider theme={createAppTheme('light')}>
            <MemoryRouter initialEntries={['/leaderboard']}>
              <Routes>
                <Route path="/leaderboard" element={<LeaderboardPage adapter={mockAdapter} />} />
                <Route path="/leaguepicker" element={<div>League Picker</div>} />
              </Routes>
            </MemoryRouter>
          </ThemeProvider>
        </QueryClientProvider>,
      );
    };

    let resolveLeagueTwo!: (value: ReturnType<typeof createLeaderboardEntry>[]) => void;
    mockedGetLeaderboard.mockImplementationOnce(() => new Promise((res) => { resolveLeagueTwo = res; }));
    rerenderWithLeague(2); // switch to league 2 — fetch starts, left pending

    let resolveLeagueThree!: (value: ReturnType<typeof createLeaderboardEntry>[]) => void;
    mockedGetLeaderboard.mockImplementationOnce(() => new Promise((res) => { resolveLeagueThree = res; }));
    rerenderWithLeague(3); // switch again before league 2's fetch ever resolved

    resolveLeagueThree([createLeaderboardEntry({ userId: '3', userName: 'LeagueThreeUser', rank: '1', total: 3, weekResults: [] })]);
    await screen.findByText('LeagueThreeUser');

    // The abandoned league-2 request finally resolves — its data must NOT overwrite league 3's,
    // which is what's actually selected now.
    resolveLeagueTwo([createLeaderboardEntry({ userId: '2', userName: 'LeagueTwoUser', rank: '1', total: 2, weekResults: [] })]);
    await new Promise((r) => setTimeout(r, 0));
    expect(screen.queryByText('LeagueTwoUser')).not.toBeInTheDocument();
    expect(screen.getByText('LeagueThreeUser')).toBeInTheDocument();
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
