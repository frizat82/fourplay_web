import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import PicksPage from '../pages/PicksPage';
import { createNflAdapter } from '../services/nflAdapter';
import { createCompetition, createCurrentWeek, createPick, createScores, createSpreadResponse } from '../test/fixtures';
import { vi } from 'vitest';
import type { NflPickDto } from '../types/picks';

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

const authState = {
  user: { userId: '123', name: 'TestUser', claims: [] },
};

const toastState = {
  push: vi.fn(),
};

vi.mock('../services/session', () => ({ useSession: () => sessionState }));
vi.mock('../services/auth', () => ({ useAuth: () => authState }));
vi.mock('../services/toast', () => ({ useToast: () => toastState }));

vi.mock('../api/espn', () => ({ getScores: vi.fn(), loadScoresWithRetry: vi.fn(), getWeekScores: vi.fn(), getLiveGames: vi.fn() }));
vi.mock('../api/league', () => ({
  addPicks: vi.fn(),
  doOddsExist: vi.fn(),
  getUserPicks: vi.fn(),
  spreadBatch: vi.fn(),
  getNflCurrentWeek: vi.fn(),
}));
vi.mock('../api/jersey', () => ({ getAllJerseys: vi.fn() }));
vi.mock('../services/spreadRelease', () => ({ getNextSpreadJob: vi.fn() }));

import { getScores, loadScoresWithRetry, getWeekScores } from '../api/espn';
import { addPicks, doOddsExist, getUserPicks, spreadBatch, getNflCurrentWeek } from '../api/league';
import { getAllJerseys } from '../api/jersey';
import { getNextSpreadJob } from '../services/spreadRelease';

const mockedGetScores = vi.mocked(getScores);
const mockedLoadScoresWithRetry = vi.mocked(loadScoresWithRetry);
const mockedGetWeekScores = vi.mocked(getWeekScores);
const mockedDoOddsExist = vi.mocked(doOddsExist);
const mockedGetUserPicks = vi.mocked(getUserPicks);
const mockedSpreadBatch = vi.mocked(spreadBatch);
const mockedAddPicks = vi.mocked(addPicks);
const mockedGetAllJerseys = vi.mocked(getAllJerseys);
const mockedGetNextSpreadJob = vi.mocked(getNextSpreadJob);
const mockedGetNflCurrentWeek = vi.mocked(getNflCurrentWeek);


const setupDefaults = async (options?: {
  week?: number;
  postSeason?: boolean;
  existingPicks?: NflPickDto[];
  oddsExist?: boolean;
  gameStarted?: boolean;
  gameDate?: string;
}) => {
  const week = options?.week ?? 2;
  const postSeason = options?.postSeason ?? false;
  const gameStarted = options?.gameStarted ?? false;
  const scores = options?.gameDate
    ? createScores({
        week,
        postSeason,
        events: [
          {
            id: '1',
            season: { year: 2024, type: 2 },
            week: { number: week },
            date: options.gameDate,
            competitions: [createCompetition({ homeTeam: 'BUF', awayTeam: 'MIA', gameStarted, date: options.gameDate })],
          },
          {
            id: '2',
            season: { year: 2024, type: 2 },
            week: { number: week },
            date: options.gameDate,
            competitions: [createCompetition({ homeTeam: 'DAL', awayTeam: 'NYG', gameStarted, date: options.gameDate })],
          },
        ],
      })
    : createScores({ week, postSeason, gameStarted });

  mockedGetScores.mockResolvedValue(scores);
  mockedGetNflCurrentWeek.mockResolvedValue(createCurrentWeek(week, postSeason));
  mockedGetWeekScores.mockResolvedValue(scores);
  mockedDoOddsExist.mockResolvedValue(options?.oddsExist ?? true);
  mockedGetUserPicks.mockResolvedValue(options?.existingPicks ?? []);
  mockedGetAllJerseys.mockResolvedValue({});
  mockedGetNextSpreadJob.mockResolvedValue(null);

  mockedSpreadBatch.mockResolvedValue({
    responses: {
      BUF: createSpreadResponse('BUF', -7, 47.5, 47.5),
      MIA: createSpreadResponse('MIA', 7, 47.5, 47.5),
      DAL: createSpreadResponse('DAL', -3.5, 44, 44),
      NYG: createSpreadResponse('NYG', 3.5, 44, 44),
    },
  });
};

const renderWithClient = (ui: React.ReactElement) => {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: Infinity } },
  });
  return { ...render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>), queryClient: client };
};

const renderPage = async () => {
  renderWithClient(<PicksPage adapter={createNflAdapter()} />);
  await screen.findByText(/^Picks$/i);
  await waitFor(() => expect(screen.queryByRole('progressbar')).toBeNull());
};

describe('PicksPage', () => {
  beforeEach(() => {
    sessionState.currentLeague = 1;
    toastState.push.mockReset();
    mockedGetScores.mockReset();
    mockedLoadScoresWithRetry.mockReset();
    mockedGetWeekScores.mockReset();
    mockedGetNflCurrentWeek.mockReset();
    mockedDoOddsExist.mockReset();
    mockedGetUserPicks.mockReset();
    mockedSpreadBatch.mockReset();
    mockedAddPicks.mockReset();
    mockedGetAllJerseys.mockReset();
    mockedGetNextSpreadJob.mockReset();
  });

  it('shows no league message when no league selected', async () => {
    sessionState.currentLeague = null;
    await setupDefaults();
    renderWithClient(<PicksPage adapter={createNflAdapter()} />);
    await screen.findByText(/Please select a league/i);
  });

  it('shows odds not posted when odds missing', async () => {
    await setupDefaults({ oddsExist: false });
    renderWithClient(<PicksPage adapter={createNflAdapter()} />);
    await screen.findByText(/Odds Not Posted/i);
  });

  // frizat: previously `showSelector = games.length > 0 || !isCurrentWeek` (plus an earlier-still
  // full-page return for the no-odds/current-week case) meant a visitor checking in before this
  // week's spreads release had no way to browse to a different week/season at all.
  it('still shows the week/season selector when the current week has no odds yet', async () => {
    await setupDefaults({ oddsExist: false });
    renderWithClient(<PicksPage adapter={createNflAdapter()} />);
    await screen.findByText(/Odds Not Posted/i);
    expect(screen.getByTestId('week-year-selector-container')).toBeInTheDocument();
  });

  it('shows picks remaining for week 2 with no existing picks', async () => {
    await setupDefaults({ week: 2 });
    await renderPage();
    expect(screen.getByText(/Picks Remaining/i)).toBeInTheDocument();
  });

  it('shows picks remaining (2) for week 2 with two existing picks', async () => {
    const existing = [createPick({ team: 'BUF' }), createPick({ team: 'DAL' })];
    await setupDefaults({ week: 2, existingPicks: existing });
    await renderPage();
    expect(screen.getByText(/Picks Remaining \(2\)/i)).toBeInTheDocument();
  });

  // frizat-d2h: toggle removed for now (likely permanent removal pending a copyright review
  // of the jersey images).
  it('never shows the Show Jerseys toggle', async () => {
    await setupDefaults({ week: 2 });
    await renderPage();
    expect(screen.queryByRole('button', { name: /show jerseys/i })).toBeNull();
  });

  // frizat-bqi: PicksPage previously had no error-state handling at all — a failed fetch just
  // silently stayed blank. Mirrors ScoresPage's existing Alert + Retry pattern.
  it('shows an error alert with a retry button when the picks query fails', async () => {
    await setupDefaults();
    mockedGetWeekScores.mockRejectedValue(new Error('network down'));
    renderWithClient(<PicksPage adapter={createNflAdapter()} />);

    const retryButton = await screen.findByRole('button', { name: /retry/i });
    expect(screen.getByText(/couldn.t load picks/i)).toBeInTheDocument();

    // Recover on retry
    mockedGetWeekScores.mockResolvedValue(createScores({ week: 2, postSeason: false, gameStarted: false }));
    await userEvent.click(retryButton);

    await waitFor(() => expect(screen.getAllByText(/BUF/i).length).toBeGreaterThan(0));
  });

  it('shows picks remaining (1) for postseason week 1 with two existing picks', async () => {
    const existing = [createPick({ team: 'BUF' }), createPick({ team: 'DAL' })];
    await setupDefaults({ week: 1, postSeason: true, existingPicks: existing });
    await renderPage();
    expect(screen.getByText(/Picks Remaining \(1\)/i)).toBeInTheDocument();
  });

  it('submit button disabled when no user picks made', async () => {
    await setupDefaults();
    await renderPage();
    const submit = screen.getByRole('button', { name: /submit pick/i });
    expect(submit).toBeDisabled();
  });

  it('submit and clear buttons enabled when user makes picks', async () => {
    await setupDefaults();
    await renderPage();

    const pickButtons = screen.getAllByRole('button', { name: /^Pick /i });
    await userEvent.click(pickButtons[0]);

    await waitFor(() => {
      const submit = screen.getByRole('button', { name: /submit pick/i });
      const clear = screen.getByRole('button', { name: /clear selected picks/i });
      expect(submit).not.toBeDisabled();
      expect(clear).not.toBeDisabled();
    });
  });

  // frizat: /style-guide audit — Submit and Clear were both contained/equal-size, reading as
  // two equal-strength CTAs when Submit is the primary action and Clear is a rare, lesser one.
  // Clear also used color="warning" as a small filled button — the exact configuration the
  // style guide documents as unreadable in both modes for pick-state buttons.
  it('demotes Clear to an outlined button so Submit reads as the primary action', async () => {
    await setupDefaults();
    await renderPage();
    const submit = screen.getByRole('button', { name: /submit pick/i });
    const clear = screen.getByRole('button', { name: /clear selected picks/i });
    expect(submit.className).toMatch(/MuiButton-contained/);
    expect(clear.className).toMatch(/MuiButton-outlined/);
    expect(clear.className).not.toMatch(/warning/i);
  });

  it('pick button toggles to picked and back', async () => {
    await setupDefaults();
    await renderPage();

    const pickButton = screen.getAllByRole('button', { name: /^Pick /i })[0];
    await userEvent.click(pickButton);
    await screen.findByRole('button', { name: /picked/i });

    const pickedButton = screen.getAllByRole('button', { name: /picked/i })[0];
    await userEvent.click(pickedButton);

    await waitFor(() => {
      expect(screen.getAllByRole('button', { name: /^Pick /i }).length).toBeGreaterThan(0);
    });
  });

  it('clear button disabled when no user picks selected', async () => {
    const existing = [createPick({ team: 'BUF' })];
    await setupDefaults({ existingPicks: existing });
    await renderPage();
    const clear = screen.getByRole('button', { name: /clear selected picks/i });
    expect(clear).toBeDisabled();
  });

  it('disables remaining pick buttons when total picks reach max', async () => {
    const existing = [createPick({ team: 'BUF' }), createPick({ team: 'DAL' })];
    await setupDefaults({ existingPicks: existing });
    await renderPage();

    const pickButtons = screen.getAllByRole('button', { name: /^Pick /i });
    await userEvent.click(pickButtons[0]);
    await userEvent.click(pickButtons[1]);

    await waitFor(() => {
      const remaining = screen.queryAllByRole('button', { name: /^Pick /i });
      if (remaining.length === 0) {
        expect(remaining.length).toBe(0);
      } else {
        remaining.forEach((btn) => expect(btn).toBeDisabled());
      }
    });
  });

  it('clears only user picks and keeps existing picks', async () => {
    const existing = [
      createPick({ team: 'BUF' }),
      createPick({ team: 'DAL' }),
    ];
    await setupDefaults({ existingPicks: existing });
    await renderPage();

    // Existing server picks show as "Locked in" (submitted state, disabled)
    const initialLocked = screen.getAllByRole('button', { name: /locked in/i }).length;
    expect(initialLocked).toBe(2);

    // Add one new user (pending) pick — it shows as "Picked" (enabled)
    const pickButton = screen.getAllByRole('button', { name: /^Pick /i })[0];
    await userEvent.click(pickButton);

    await waitFor(() => {
      expect(screen.getAllByRole('button', { name: /picked/i }).length).toBe(1);
    });

    await userEvent.click(screen.getByRole('button', { name: /clear selected picks/i }));

    // After clearing, user pick gone; existing locked picks remain
    await waitFor(() => {
      expect(screen.queryAllByRole('button', { name: /picked/i }).length).toBe(0);
      expect(screen.getAllByRole('button', { name: /locked in/i }).length).toBe(2);
    });
  });

  it('submit picks clears user picks and reloads existing', async () => {
    await setupDefaults();
    mockedAddPicks.mockResolvedValue(1);
    mockedGetUserPicks.mockResolvedValue([createPick({ team: 'BUF' })]);

    await renderPage();
    const pickButton = screen.getAllByRole('button', { name: /^Pick /i })[0];
    await userEvent.click(pickButton);

    await userEvent.click(screen.getByRole('button', { name: /submit pick/i }));

    await waitFor(() => {
      expect(mockedAddPicks).toHaveBeenCalledTimes(1);
      const submit = screen.getByRole('button', { name: /submit pick/i });
      const clear = screen.getByRole('button', { name: /clear selected picks/i });
      expect(submit).toBeDisabled();
      expect(clear).toBeDisabled();
    });
  });

  it('season selector keeps the current season navigable after viewing a historical season', async () => {
    // Regression: navigating into a past season used to permanently shrink the selector's
    // range to "minSeason..whatever season you're viewing", trapping the user in the past —
    // loadHistoricalGames returns maxSeason: <the season being viewed>, and PicksPage was
    // re-deriving maxSeason from that on every render instead of remembering the real ceiling
    // from the last current-week load.
    await setupDefaults({ week: 2 }); // current week resolves to season 2024 (fixture default)
    // getWeekScores now serves both the current week (season 2024) and the historical navigation
    // below (season 2022) — differentiate by the requested year so the initial current-week load
    // isn't overwritten by the season-2022 fixture before the snapshot is even captured.
    mockedGetWeekScores.mockImplementation(async (_week: number, year: number) => year === 2022
      ? createScores({ week: 2, seasonYear: 2022, gameStarted: false })
      : createScores({ week: 2, postSeason: false, gameStarted: false }));

    await renderPage();

    // Open the season selector (1st of 3 comboboxes: season, week, type) and jump to 2022.
    await userEvent.click(screen.getAllByRole('combobox')[0]);
    await userEvent.click(screen.getByRole('option', { name: '2022 Season' }));
    await waitFor(() => expect(screen.getByText('2022 Season')).toBeInTheDocument());

    // The current season (2024) must still be a selectable option — not dropped from the range.
    await userEvent.click(screen.getAllByRole('combobox')[0]);
    expect(screen.getByRole('option', { name: '2024 Season' })).toBeInTheDocument();
  });

  it('locks picks when existing picks equal max allowed', async () => {
    const existing = [
      createPick({ team: 'BUF' }),
      createPick({ team: 'DAL' }),
      createPick({ team: 'MIA' }),
      createPick({ team: 'NYG' }),
    ];
    await setupDefaults({ existingPicks: existing });
    await renderPage();

    expect(screen.queryByRole('button', { name: /submit pick/i })).toBeNull();
    expect(screen.queryByRole('button', { name: /clear selected picks/i })).toBeNull();
  });

  it('shows "Submit picks before gametime" header text', async () => {
    await setupDefaults();
    await renderPage();
    expect(screen.getByText(/Submit picks before gametime/i)).toBeInTheDocument();
  });

  it('pick buttons enabled before game kickoff time', async () => {
    const futureDate = new Date(Date.now() + 2 * 60 * 60 * 1000).toISOString();
    await setupDefaults({ gameDate: futureDate });
    await renderPage();

    const pickButton = screen.getAllByRole('button', { name: /^Pick /i })[0];
    expect(pickButton).not.toBeDisabled();
  });

  it('pick buttons disabled when game kickoff time has passed (even if ESPN status still scheduled)', async () => {
    const pastDate = new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString();
    await setupDefaults({ gameDate: pastDate, gameStarted: false });
    await renderPage();

    screen.getAllByRole('button', { name: /^Pick /i }).forEach((btn) => {
      expect(btn).toBeDisabled();
    });
  });

  it('postseason week 1 displays wild card title', async () => {
    await setupDefaults({ week: 1, postSeason: true });
    await renderPage();
    expect(screen.getAllByText(/Wild Card/i).length).toBeGreaterThan(0);
  });

  it('postseason week 2 displays divisional round title', async () => {
    await setupDefaults({ week: 2, postSeason: true });
    await renderPage();
    expect(screen.getAllByText(/Divisional Round/i).length).toBeGreaterThan(0);
  });

  it('postseason week 3 displays conference championship title', async () => {
    await setupDefaults({ week: 3, postSeason: true });
    await renderPage();
    expect(screen.getAllByText(/Conference Championship/i).length).toBeGreaterThan(0);
  });

  it('postseason week 4 displays super bowl title', async () => {
    await setupDefaults({ week: 4, postSeason: true });
    await renderPage();
    await waitFor(() => expect(screen.getAllByText(/Super Bowl/i).length).toBeGreaterThan(0));
  });

  // frizat: mirrors the equivalent ScoresPage test — same isPlaceholderData fix, same regression
  // (Previous/Next navigation "freezing" on the old week's stale content with no loading signal).
  it('shows the skeleton, not a frozen stale week, while navigating to a previous week', async () => {
    await setupDefaults({ week: 3 });
    await renderPage();
    await waitFor(() => expect(screen.getAllByText(/BUF/i).length).toBeGreaterThan(0));

    let resolveHistorical!: (value: Awaited<ReturnType<typeof getWeekScores>>) => void;
    mockedGetWeekScores.mockImplementationOnce(() => new Promise(resolve => { resolveHistorical = resolve; }));

    await userEvent.click(screen.getByRole('button', { name: /previous/i }));

    await waitFor(() => expect(screen.queryByRole('button', { name: /previous/i })).toBeNull());
    expect(document.querySelectorAll('.MuiSkeleton-root').length).toBeGreaterThan(0);

    resolveHistorical(createScores({ week: 2, postSeason: false }));

    await waitFor(() => expect(screen.getByRole('button', { name: /previous/i })).toBeInTheDocument());
    expect(document.querySelectorAll('.MuiSkeleton-root').length).toBe(0);
  });

  // frizat: regression caught by /code-review on the isPlaceholderData fix above — see the
  // equivalent ScoresPage test for the full explanation (enabled:false queries never leave
  // "placeholder" state, so this must not trap the page on the skeleton forever).
  it('falls through to the no-league message, not a stuck skeleton, when currentLeague goes from set to null after a successful load', async () => {
    await setupDefaults();
    const { rerender, queryClient } = renderWithClient(<PicksPage adapter={createNflAdapter()} />);
    await screen.findByText(/^Picks$/i);
    await waitFor(() => expect(screen.getAllByText(/BUF/i).length).toBeGreaterThan(0));

    sessionState.currentLeague = null;
    rerender(
      <QueryClientProvider client={queryClient}>
        <PicksPage adapter={createNflAdapter()} />
      </QueryClientProvider>,
    );

    await waitFor(() => expect(screen.getByText(/Please select a league/i)).toBeInTheDocument());
    expect(document.querySelectorAll('.MuiSkeleton-root').length).toBe(0);
  });

  it('regular season hides over/under buttons', async () => {
    await setupDefaults({ week: 2, postSeason: false });
    await renderPage();
    expect(screen.queryByRole('button', { name: /^Over$/i })).toBeNull();
    expect(screen.queryByRole('button', { name: /^Under$/i })).toBeNull();
  });

  it('postseason shows over/under buttons and toggles', async () => {
    await setupDefaults({ week: 1, postSeason: true });
    await renderPage();

    const overButton = screen.getAllByRole('button', { name: /^Over$/i })[0];
    await userEvent.click(overButton);
    await screen.findByRole('button', { name: /^Over 47\.5 ✓$/i });

    const underButton = screen.getAllByRole('button', { name: /^Under$/i })[0];
    await userEvent.click(underButton);
    await screen.findByRole('button', { name: /^Under 47\.5 ✓$/i });
  });

  it('postseason allows selecting spread and over picks together', async () => {
    await setupDefaults({ week: 1, postSeason: true });
    await renderPage();

    const pickButton = screen.getAllByRole('button', { name: /^Pick /i })[0];
    await userEvent.click(pickButton);
    await screen.findByRole('button', { name: /picked/i });

    const overButton = screen.getAllByRole('button', { name: /^Over$/i })[0];
    await userEvent.click(overButton);
    await screen.findByRole('button', { name: /^Over 47\.5 ✓$/i });
  });

  it('renders a dedicated over/under control block per postseason matchup', async () => {
    await setupDefaults({ week: 1, postSeason: true });
    await renderPage();

    const controls = screen.getAllByTestId('over-under-controls');
    expect(controls.length).toBeGreaterThan(0);

    controls.forEach((control) => {
      expect(within(control).getByRole('button', { name: /^Over$/i })).toBeInTheDocument();
      expect(within(control).getByRole('button', { name: /^Under$/i })).toBeInTheDocument();
    });
  });

  // ── Background refresh behavior (frizat-mon.5) ─────────────────────────────
  // The poll refetch must be invisible: no spinner, and pending (unsubmitted)
  // selections must survive. NFL poll interval is 300s (nflAdapter.pollIntervalMs).
  describe('background poll refresh', () => {
    const POLL_MS = 300_000;

    afterEach(() => {
      vi.useRealTimers();
    });

    it('preserves pending picks across a background poll refetch', async () => {
      vi.useFakeTimers({ shouldAdvanceTime: true });
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      await setupDefaults();
      await renderPage();

      await user.click(screen.getAllByRole('button', { name: /^Pick /i })[0]);
      await screen.findByRole('button', { name: /picked/i });

      await act(async () => {
        await vi.advanceTimersByTimeAsync(POLL_MS);
      });

      expect(screen.getAllByRole('button', { name: /picked/i }).length).toBe(1);
    });

    it('does not show the full-page spinner during a background refetch', async () => {
      vi.useFakeTimers({ shouldAdvanceTime: true });
      await setupDefaults();
      await renderPage();

      // Hold the next fetch in-flight forever so we can observe mid-refetch UI
      mockedGetWeekScores.mockImplementation(() => new Promise(() => {}));

      await act(async () => {
        await vi.advanceTimersByTimeAsync(POLL_MS);
      });

      // Grid stays mounted, no spinner replaces the page
      expect(screen.queryByRole('progressbar')).toBeNull();
      expect(screen.getAllByRole('button', { name: /^Pick /i }).length).toBeGreaterThan(0);
    });

    it('drops a pending pick with a toast when its game locks during refetch', async () => {
      vi.useFakeTimers({ shouldAdvanceTime: true });
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      const futureDate = new Date(Date.now() + 2 * 60 * 60 * 1000).toISOString();
      await setupDefaults({ gameDate: futureDate });
      await renderPage();

      await user.click(screen.getAllByRole('button', { name: /^Pick /i })[0]);
      await screen.findByRole('button', { name: /picked/i });

      // Next poll: same games but kickoff has passed
      const pastDate = new Date(Date.now() - 5 * 60 * 1000).toISOString();
      const startedScores = createScores({
        week: 2,
        postSeason: false,
        events: [
          {
            id: '1',
            season: { year: 2024, type: 2 },
            week: { number: 2 },
            date: pastDate,
            competitions: [createCompetition({ homeTeam: 'BUF', awayTeam: 'MIA', gameStarted: true, date: pastDate })],
          },
          {
            id: '2',
            season: { year: 2024, type: 2 },
            week: { number: 2 },
            date: pastDate,
            competitions: [createCompetition({ homeTeam: 'DAL', awayTeam: 'NYG', gameStarted: true, date: pastDate })],
          },
        ],
      });
      mockedGetWeekScores.mockResolvedValue(startedScores);

      await act(async () => {
        await vi.advanceTimersByTimeAsync(POLL_MS);
      });

      expect(screen.queryByRole('button', { name: /picked/i })).toBeNull();
      expect(toastState.push).toHaveBeenCalledWith(
        expect.stringMatching(/game.*(started|kicked off)/i),
        expect.anything(),
      );
    });
  });
});
