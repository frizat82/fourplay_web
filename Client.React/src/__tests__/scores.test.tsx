import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import ScoresPage from '../pages/ScoresPage';
import { createNflAdapter } from '../services/nflAdapter';
import { createCfbAdapter } from '../services/cfbAdapter';
import { createCurrentWeek, createPick, createScores, createSpreadResponse, createCompetition } from '../test/fixtures';
import { vi } from 'vitest';
import type { NflPickDto } from '../types/picks';

// jsdom has no EventSource — ScoresPage opens one whenever hasActiveGames is true (SSE, primary
// live-update path for NFL). Fallback polling (this file's actual focus) still needs it defined
// so the effect doesn't throw and fall out of the tree.
class MockEventSource {
  onmessage: (() => void) | null = null;
  onerror: (() => void) | null = null;
  close() {}
}
vi.stubGlobal('EventSource', MockEventSource);

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
vi.mock('../api/espn', () => ({
  getScores: vi.fn(), loadScoresWithRetry: vi.fn(), getWeekScores: vi.fn(), getLiveGames: vi.fn(),
  getCfbScoresForSlate: vi.fn(), getCfbLiveGames: vi.fn(),
}));
vi.mock('../api/league', () => ({
  doOddsExist: vi.fn(), getLeaguePicks: vi.fn(), spreadBatch: vi.fn(),
  addPicks: vi.fn(), getUserPicks: vi.fn(), getNflCurrentWeek: vi.fn(),
}));
vi.mock('../api/cfb', () => ({
  getCfbCurrentSlate: vi.fn(), getCfbSlates: vi.fn(), getCfbSpreads: vi.fn(),
  getCfbScores: vi.fn(), getCfbUserPicks: vi.fn(), getCfbAllPicks: vi.fn(),
  addCfbPicks: vi.fn(), deleteCfbPicks: vi.fn(),
}));
vi.mock('../services/spreadRelease', () => ({ getNextSpreadJob: vi.fn().mockResolvedValue(null) }));
const toastPush = vi.fn();
vi.mock('../services/toast', () => ({ useToast: () => ({ push: toastPush }) }));

import { getLiveGames, getWeekScores, getCfbScoresForSlate, getCfbLiveGames } from '../api/espn';
import { doOddsExist, getLeaguePicks, spreadBatch, getNflCurrentWeek } from '../api/league';
import { getCfbCurrentSlate, getCfbSlates, getCfbSpreads, getCfbScores, getCfbAllPicks } from '../api/cfb';

const mockedGetLiveGames = vi.mocked(getLiveGames);
const mockedGetWeekScores = vi.mocked(getWeekScores);
const mockedDoOddsExist = vi.mocked(doOddsExist);
const mockedGetLeaguePicks = vi.mocked(getLeaguePicks);
const mockedGetNflCurrentWeek = vi.mocked(getNflCurrentWeek);
const mockedGetCfbSlates = vi.mocked(getCfbSlates);
const mockedGetCfbSpreads = vi.mocked(getCfbSpreads);
const mockedGetCfbScores = vi.mocked(getCfbScores);
const mockedGetCfbAllPicks = vi.mocked(getCfbAllPicks);
const mockedGetCfbScoresForSlate = vi.mocked(getCfbScoresForSlate);
const mockedGetCfbLiveGames = vi.mocked(getCfbLiveGames);
const mockedSpreadBatch = vi.mocked(spreadBatch);
const mockedGetCfbCurrentSlate = vi.mocked(getCfbCurrentSlate);


// BUF home (24-10), spread -7: homeCovers = 24+(-7)=17 > 10 ✓ (BUF covers → green)
// MIA away: !homeCovers → red; Over at 47.5: 24+10=34 < 47.5 → Under wins
const SPREAD_RESPONSES = {
  BUF: createSpreadResponse('BUF', -7, 47.5, 47.5),
  MIA: createSpreadResponse('MIA', 7, 47.5, 47.5),
  DAL: createSpreadResponse('DAL', -3, 47.5, 47.5),
  NYG: createSpreadResponse('NYG', 3, 47.5, 47.5),
};

function makeScores(week: number, postSeason: boolean, gameStarted: boolean) {
  const bufComp = createCompetition({ homeTeam: 'BUF', awayTeam: 'MIA', homeScore: 24, awayScore: 10, gameStarted });
  const dalComp = createCompetition({ homeTeam: 'DAL', awayTeam: 'NYG', homeScore: 28, awayScore: 17, gameStarted });
  return createScores({ week, postSeason, events: [
    { id: '1', season: { year: 2024, type: postSeason ? 3 : 2 }, week: { number: week }, date: new Date().toISOString(), competitions: [bufComp] },
    { id: '2', season: { year: 2024, type: postSeason ? 3 : 2 }, week: { number: week }, date: new Date().toISOString(), competitions: [dalComp] },
  ]});
}

const setupDefaults = async (options?: {
  week?: number; postSeason?: boolean; gameStarted?: boolean;
  oddsExist?: boolean; picks?: NflPickDto[];
}) => {
  const week = options?.week ?? 2;
  const postSeason = options?.postSeason ?? false;
  const gameStarted = options?.gameStarted ?? true;
  mockedGetNflCurrentWeek.mockResolvedValue(createCurrentWeek(week, postSeason));
  mockedGetWeekScores.mockResolvedValue(makeScores(week, postSeason, gameStarted));
  mockedGetLiveGames.mockResolvedValue([]);
  mockedDoOddsExist.mockResolvedValue(options?.oddsExist ?? true);
  mockedGetLeaguePicks.mockResolvedValue(options?.picks ?? []);
  mockedSpreadBatch.mockResolvedValue({ responses: SPREAD_RESPONSES });
};

const renderWithClient = (ui: React.ReactElement, client?: QueryClient) => {
  const queryClient = client ?? new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: Infinity } },
  });
  return { ...render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>), queryClient };
};

const renderPage = async () => {
  const utils = renderWithClient(<ScoresPage adapter={createNflAdapter()} />);
  await screen.findByText(/Scores/i);
  await waitFor(() => expect(screen.queryByRole('progressbar')).toBeNull());
  return utils;
};

describe('ScoresPage', () => {
  beforeEach(() => {
    sessionState.currentLeague = 1;
    vi.clearAllMocks();
  });

  it('shows no league message when no league selected', async () => {
    sessionState.currentLeague = null;
    await setupDefaults();
    renderWithClient(<ScoresPage adapter={createNflAdapter()} />);
    await screen.findByText(/Please select a league/i);
  });

  it('shows odds not posted when odds missing', async () => {
    await setupDefaults({ oddsExist: false });
    renderWithClient(<ScoresPage adapter={createNflAdapter()} />);
    await screen.findByText(/Odds Not Posted/i);
  });

  // frizat: previously the current-week/no-odds case was a full-page early return that skipped
  // WeekYearSelector entirely — a visitor checking in before this week's spreads release had no
  // way to browse to a different week/season at all.
  it('still shows the week/season selector when the current week has no odds yet', async () => {
    await setupDefaults({ oddsExist: false });
    renderWithClient(<ScoresPage adapter={createNflAdapter()} />);
    await screen.findByText(/Odds Not Posted/i);
    expect(screen.getByTestId('week-year-selector-container')).toBeInTheDocument();
  });

  it('shows week title when scores available', async () => {
    await setupDefaults({ week: 5 });
    await renderPage();
    expect(screen.getAllByText(/Week 5/i).length).toBeGreaterThan(0);
  });

  // frizat: the Scores page's Share button only ever shared window.location.href — a bare link to
  // the site, no actual content. Removed; the Leaderboard's Share button (ShareableStandingsCard,
  // a branded standings image) is the one that's worth sharing and stays.
  it('has no Share button', async () => {
    await setupDefaults({ week: 5 });
    await renderPage();

    expect(screen.queryByRole('button', { name: /^share$/i })).not.toBeInTheDocument();
  });

  it('shows postseason wild card title', async () => {
    await setupDefaults({ week: 1, postSeason: true });
    await renderPage();
    expect(screen.getAllByText(/Wild Card/i).length).toBeGreaterThan(0);
  });

  it('displays scores when game completed', async () => {
    await setupDefaults({ gameStarted: true });
    await renderPage();
    expect(screen.getByText('24')).toBeInTheDocument();
    expect(screen.getByText('10')).toBeInTheDocument();
  });

  it('shows person icon buttons when picks exist', async () => {
    const picks = [createPick({ team: 'BUF' })];
    await setupDefaults({ picks, gameStarted: true });
    await renderPage();
    expect(screen.getAllByTestId('PersonIcon').length).toBeGreaterThan(0);
  });

  it('displays team names', async () => {
    await setupDefaults();
    await renderPage();
    expect(screen.getAllByText(/BUF/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/MIA/i).length).toBeGreaterThan(0);
  });

  it('renders multiple games', async () => {
    await setupDefaults();
    await renderPage();
    expect(screen.getAllByText(/DAL/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/NYG/i).length).toBeGreaterThan(0);
  });

  it('postseason displays over/under icons', async () => {
    await setupDefaults({ week: 1, postSeason: true });
    await renderPage();
    expect(screen.getAllByTestId('ArrowCircleUpIcon').length).toBeGreaterThan(0);
    expect(screen.getAllByTestId('ArrowCircleDownIcon').length).toBeGreaterThan(0);
  });

  // frizat: /style-guide audit — each pick-result row encoded win/loss three redundant ways at
  // once: this shield icon (shape AND color both changed), the IconButton's own color, and the
  // Badge's color. Drop the standalone shield icon; the badge/icon-button pair alone is the
  // signal now (same "background/color is the only signal" resolution as the Matrix view).
  it('does not render a redundant win/loss shield icon — the pick badge/button color alone carries it', async () => {
    const picks = [createPick({ team: 'BUF', userName: 'OtherUser', userId: '456' })];
    await setupDefaults({ picks, gameStarted: true });
    await renderPage();
    expect(screen.queryByTestId('GppGoodIcon')).not.toBeInTheDocument();
    expect(screen.queryByTestId('GppBadIcon')).not.toBeInTheDocument();
    expect(screen.queryByTestId('GppMaybeIcon')).not.toBeInTheDocument();
  });

  // frizat: same redundancy on the O/U row — the up/down arrows were colored success/error on
  // top of the Badge/IconButton pairs on either side already showing the identical win/loss
  // state. Arrows now stay a fixed neutral color; the badges are the one signal.
  it('keeps the Over/Under arrows a neutral color regardless of win/loss — the badges already carry that signal', async () => {
    // BUF 24, MIA 10 → total 34 < O/U 47.5 → Under wins (see makeScores comment above)
    await setupDefaults({ week: 1, postSeason: true, gameStarted: true });
    await renderPage();
    const upArrow = screen.getAllByTestId('ArrowCircleUpIcon')[0];
    const downArrow = screen.getAllByTestId('ArrowCircleDownIcon')[0];
    expect(upArrow).not.toHaveStyle({ color: 'rgb(211, 47, 47)' }); // MUI error.main
    expect(upArrow).not.toHaveStyle({ color: 'rgb(46, 125, 50)' }); // MUI success.main
    expect(downArrow).not.toHaveStyle({ color: 'rgb(211, 47, 47)' });
    expect(downArrow).not.toHaveStyle({ color: 'rgb(46, 125, 50)' });
  });

  // frizat: /style-guide audit — "Show As Matrix" defaulted to unstyled contained (reads as
  // primary navy, near-inert next to body text) while "Show Only My Picks" used contained
  // secondary (the brand orange reserved for real CTAs like Share) — two equal-weight view
  // filters shouldn't have one wearing the brand accent. Both are the same neutral treatment now.
  it('gives the Matrix and My-Picks view toggles matching weight, neither wearing the brand-CTA color', async () => {
    const picks = [createPick({ team: 'BUF' })];
    await setupDefaults({ picks, gameStarted: true });
    await renderPage();

    const matrixButton = screen.getByRole('button', { name: /show as matrix/i });
    const myPicksButton = screen.getByRole('button', { name: /show only my picks/i });
    expect(matrixButton.className).toMatch(/MuiButton-outlinedInfo/);
    expect(myPicksButton.className).toMatch(/MuiButton-outlinedInfo/);
    expect(matrixButton.className).not.toMatch(/Secondary/);
    expect(myPicksButton.className).not.toMatch(/Secondary/);
  });

  // /code-review caught that the removed shield icon was the ONLY win/loss signal for a team
  // nobody in the league picked — the badge/icon-button pair used pickCount === 0 to both hide
  // the count bubble AND disable the button, and MUI's disabled state flattens `color` to gray
  // regardless of the success/error prop. Disabled must now track "not decided yet" only, so the
  // outcome color still shows even with zero picks.
  it('keeps the pick icon enabled and colored by outcome even when nobody in the league picked that side', async () => {
    await setupDefaults({ picks: [], gameStarted: true });
    const { getByTestId } = await renderPage();
    const badge = getByTestId('badge-BUF-spread');
    const button = within(badge).getByRole('button');
    expect(button).not.toBeDisabled();
    expect(button.className).toMatch(/colorSuccess/);
  });

  it('spread badge is info when current user has a pick', async () => {
    const picks = [createPick({ team: 'BUF' })];
    await setupDefaults({ picks, gameStarted: true });
    const { getByTestId } = await renderPage();
    expect(getByTestId('badge-BUF-spread')).toHaveAttribute('data-tone', 'info');
  });

  it('spread badge is success when other user pick covers (BUF -7, wins 24-10)', async () => {
    const picks = [createPick({ team: 'BUF', userName: 'OtherUser', userId: '456' })];
    await setupDefaults({ picks, gameStarted: true });
    const { getByTestId } = await renderPage();
    expect(getByTestId('badge-BUF-spread')).toHaveAttribute('data-tone', 'success');
  });

  it('spread badge is error when other user pick does not cover (MIA +7, loses 10-24)', async () => {
    const picks = [createPick({ team: 'MIA', userName: 'OtherUser', userId: '456' })];
    await setupDefaults({ picks, gameStarted: true });
    const { getByTestId } = await renderPage();
    expect(getByTestId('badge-MIA-spread')).toHaveAttribute('data-tone', 'error');
  });

  it('current user picks always show info badge regardless of result', async () => {
    const picks = [createPick({ team: 'BUF' }), createPick({ team: 'MIA' })];
    await setupDefaults({ picks, gameStarted: true });
    const { getByTestId } = await renderPage();
    expect(getByTestId('badge-BUF-spread')).toHaveAttribute('data-tone', 'info');
    expect(getByTestId('badge-MIA-spread')).toHaveAttribute('data-tone', 'info');
  });

  it('show only my picks hides games the user did not pick', async () => {
    // User picked BUF (home, game 1) but not DAL/NYG (game 2)
    const picks = [createPick({ team: 'BUF', userId: '123' })];
    await setupDefaults({ picks, gameStarted: true });
    await renderPage();

    // Both games visible initially
    expect(screen.getAllByText(/BUF/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/DAL/i).length).toBeGreaterThan(0);

    await userEvent.click(screen.getByRole('button', { name: /show only my picks/i }));

    await waitFor(() => {
      expect(screen.getAllByText(/BUF/i).length).toBeGreaterThan(0);
    });
    // DAL game should be hidden
    expect(screen.queryByText(/^DAL$/)).toBeNull();
  });

  it('show only my picks shows all games when toggled back', async () => {
    const picks = [createPick({ team: 'BUF', userId: '123' })];
    await setupDefaults({ picks, gameStarted: true });
    await renderPage();

    await userEvent.click(screen.getByRole('button', { name: /show only my picks/i }));
    await waitFor(() => expect(screen.queryByText(/^DAL$/)).toBeNull());

    await userEvent.click(screen.getByRole('button', { name: /show all games/i }));
    await waitFor(() => expect(screen.getAllByText(/DAL/i).length).toBeGreaterThan(0));
  });

  it('show only my picks shows empty message when user has no picks', async () => {
    await setupDefaults({ picks: [], gameStarted: true });
    await renderPage();

    await userEvent.click(screen.getByRole('button', { name: /show only my picks/i }));

    await waitFor(() => {
      expect(screen.getByText(/haven.t made any picks/i)).toBeInTheDocument();
    });
  });

  // ── React Query migration (frizat-ulm) ──────────────────────────────────
  describe('background poll refresh', () => {
    const POLL_MS = 300_000;

    afterEach(() => {
      vi.useRealTimers();
    });

    it('shows stale scores during a background refetch, no spinner', async () => {
      vi.useFakeTimers({ shouldAdvanceTime: true });
      // hasActiveGames must be true — an already-final fixture polls at 4x this interval,
      // so advancing by POLL_MS alone would never trigger a refetch at all.
      const liveComp = createCompetition({
        homeTeam: 'BUF', awayTeam: 'MIA', homeScore: 24, awayScore: 10,
        liveStatus: { name: 'status_in_progress', period: 2, displayClock: '5:00' },
      });
      mockedGetNflCurrentWeek.mockResolvedValue(createCurrentWeek(2));
      mockedGetWeekScores.mockResolvedValue(createScores({
        week: 2, postSeason: false,
        events: [{ id: '1', season: { year: 2024, type: 2 }, week: { number: 2 }, date: new Date().toISOString(), competitions: [liveComp] }],
      }));
      mockedGetLiveGames.mockResolvedValue([]);
      mockedDoOddsExist.mockResolvedValue(true);
      mockedGetLeaguePicks.mockResolvedValue([]);
      mockedSpreadBatch.mockResolvedValue({ responses: SPREAD_RESPONSES });
      await renderPage();
      expect(screen.getAllByText(/BUF/i).length).toBeGreaterThan(0);

      // Hold the next fetch in-flight forever so we can observe mid-refetch UI
      mockedGetWeekScores.mockImplementation(() => new Promise(() => {}));

      await act(async () => {
        await vi.advanceTimersByTimeAsync(POLL_MS);
      });

      // Stale data stays on screen, no spinner replaces the page
      expect(screen.queryByRole('progressbar')).toBeNull();
      expect(screen.getAllByText(/BUF/i).length).toBeGreaterThan(0);
    });

    it('selecting the literal current week from the dropdown (not the "Current Week" button) keeps polling active', async () => {
      vi.useFakeTimers({ shouldAdvanceTime: true });
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      // hasActiveGames must be true — proves polling only continues if weekState correctly
      // routed back to the live query (isCurrentWeek), not a one-off historical fetch, which
      // would disable refetchInterval entirely and silently freeze this in-progress game.
      const liveComp = createCompetition({
        homeTeam: 'BUF', awayTeam: 'MIA', homeScore: 24, awayScore: 10,
        liveStatus: { name: 'status_in_progress', period: 2, displayClock: '5:00' },
      });
      mockedGetNflCurrentWeek.mockResolvedValue(createCurrentWeek(2));
      // getWeekScores now serves BOTH the current week (2, via loadCurrentScores) and historical
      // navigation (week 5) — differentiate by the requested week, same as the real backend would.
      mockedGetWeekScores.mockImplementation(async (week: number) => week === 5
        ? makeScores(5, false, true)
        : createScores({
            week: 2, postSeason: false,
            events: [{ id: '1', season: { year: 2024, type: 2 }, week: { number: 2 }, date: new Date().toISOString(), competitions: [liveComp] }],
          }));
      mockedGetLiveGames.mockResolvedValue([]);
      mockedDoOddsExist.mockResolvedValue(true);
      mockedGetLeaguePicks.mockResolvedValue([]);
      mockedSpreadBatch.mockResolvedValue({ responses: SPREAD_RESPONSES });
      await renderPage();
      expect(screen.getAllByText(/BUF/i).length).toBeGreaterThan(0);

      // Navigate away to week 5 (a real historical fetch), then back to week 2 via the dropdown
      // — week 2 is literally the current week's own identity (setupDefaults default), so
      // returning to it must route back to weekState=null, not stay on the historical path.
      await user.click(screen.getAllByRole('combobox')[1]);
      await user.click(screen.getByRole('option', { name: /week 5/i }));
      await waitFor(() => expect(screen.getAllByText(/DAL/i).length).toBeGreaterThan(0));

      await user.click(screen.getAllByRole('combobox')[1]);
      await user.click(screen.getByRole('option', { name: /week 2/i }));
      await waitFor(() => expect(screen.getAllByText(/BUF/i).length).toBeGreaterThan(0));

      // Clear AFTER the one-off settle call for "week 2" itself (loadCurrentScores' own
      // getWeekScores(2, ...) call when weekState routes back to the live query) — that call is
      // unavoidable and NOT what proves polling stayed active. Only a FURTHER call, after
      // advancing past the poll interval, proves that.
      mockedGetWeekScores.mockClear();

      await act(async () => {
        await vi.advanceTimersByTimeAsync(POLL_MS);
      });
      expect(mockedGetWeekScores).toHaveBeenCalled();
    });
  });

  it('season selector keeps the current season navigable after viewing a historical season', async () => {
    // Regression (mirrors the equivalent PicksPage test): navigating into a past season used to
    // permanently shrink the selector's range to "minSeason..whatever season you're viewing" —
    // loadHistoricalScores returns maxSeason: <the season being viewed>, so re-deriving the
    // ceiling from the active query's data on every render collapses it instead of remembering
    // the real ceiling from the last current-week load (currentWeekSnapshot).
    await setupDefaults({ week: 2 }); // current week resolves to season 2024 (fixture default)
    // getWeekScores now serves both the current week (season 2024) and the historical navigation
    // below (season 2022) — differentiate by the requested year so the initial current-week load
    // isn't overwritten by the season-2022 fixture before the snapshot is even captured.
    mockedGetWeekScores.mockImplementation(async (_week: number, year: number) => year === 2022
      ? createScores({ week: 2, seasonYear: 2022, gameStarted: true })
      : makeScores(2, false, true));
    await renderPage();

    await userEvent.click(screen.getAllByRole('combobox')[0]);
    await userEvent.click(screen.getByRole('option', { name: '2022 Season' }));
    await waitFor(() => expect(screen.getByText('2022 Season')).toBeInTheDocument());

    // The current season (2024) must still be a selectable option — not dropped from the range.
    await userEvent.click(screen.getAllByRole('combobox')[0]);
    expect(screen.getByRole('option', { name: '2024 Season' })).toBeInTheDocument();
  });

  it('shows an error alert with a retry button when the scores query fails', async () => {
    await setupDefaults();
    mockedGetWeekScores.mockRejectedValue(new Error('network down'));
    renderWithClient(<ScoresPage adapter={createNflAdapter()} />);

    const retryButton = await screen.findByRole('button', { name: /retry/i });
    expect(screen.getByText(/couldn.t load scores/i)).toBeInTheDocument();

    // Recover on retry
    mockedGetWeekScores.mockResolvedValue(makeScores(2, false, true));
    await userEvent.click(retryButton);

    await waitFor(() => expect(screen.getAllByText(/BUF/i).length).toBeGreaterThan(0));
  });

  it('keys the scores query by adapter.sport, so NFL and CFB never share a cache entry', async () => {
    await setupDefaults();
    const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: Infinity } } });
    const { rerender } = renderWithClient(<ScoresPage adapter={createNflAdapter()} />, client);
    await screen.findByText(/Scores/i);
    await waitFor(() => expect(screen.queryByRole('progressbar')).toBeNull());
    expect(screen.getAllByText(/BUF/i).length).toBeGreaterThan(0);

    // CFB gets its own real, resolved dataset with entirely different teams (MICH/PSU — CFB
    // has no BUF/MIA). placeholderData: keepPreviousData legitimately shows NFL's stale BUF/MIA
    // as a transient placeholder while CFB's own query is pending (that's the point of
    // keepPreviousData — no spinner flash on navigation) — the real assertion for "separate
    // cache entries" is that once CFB's OWN query settles, ITS data (MICH/PSU) is what renders,
    // not that NFL's cache entry got clobbered or that a spinner necessarily appears.
    const cfbSlate = { id: 1, season: 2025, slateNumber: 8, label: 'Week 8', slateType: 'RegularSeason', startDate: '2025-10-11', endDate: '2025-10-18' };
    const cfbSpread = { id: 1, cfbSlateId: 1, homeTeam: 'MICH', awayTeam: 'PSU', homeTeamSpread: -3.5, awayTeamSpread: 3.5, overUnder: 44.5, gameTime: '2025-10-11T20:00:00Z', dateCreated: '2025-10-09T14:00:00Z', homeTeamRank: null, awayTeamRank: null };
    mockedGetCfbCurrentSlate.mockResolvedValue(cfbSlate);
    mockedGetCfbSlates.mockResolvedValue([cfbSlate]);
    mockedGetCfbSpreads.mockResolvedValue([cfbSpread]);
    mockedGetCfbScores.mockResolvedValue([]);
    mockedGetCfbAllPicks.mockResolvedValue([]);
    mockedGetCfbScoresForSlate.mockResolvedValue({ leagues: [], season: { year: 2025, type: 2 }, week: { number: 8 }, events: [] });
    mockedGetCfbLiveGames.mockResolvedValue([]);

    rerender(
      <QueryClientProvider client={client}>
        <ScoresPage adapter={createCfbAdapter()} />
      </QueryClientProvider>,
    );

    await waitFor(() => expect(screen.getAllByText(/MICH/i).length).toBeGreaterThan(0));
    expect(screen.queryByText(/BUF/i)).toBeNull();
  });

  // frizat: reported on iOS as Previous/Next "freezing" — placeholderData: keepPreviousData
  // showed the OLD week with zero loading indicator until the new week resolved. isLoading alone
  // never re-triggers on navigation (only ever true on the very first load), so the guard must
  // also check isPlaceholderData — the flag that IS true specifically while stale data from a
  // previous query key is being shown in place of the new key's still-pending result.
  it('shows the skeleton, not a frozen stale week, while navigating to a previous week', async () => {
    await setupDefaults({ week: 3 });
    await renderPage();
    await waitFor(() => expect(screen.getAllByText(/BUF/i).length).toBeGreaterThan(0));

    let resolveHistorical!: (value: Awaited<ReturnType<typeof getWeekScores>>) => void;
    mockedGetWeekScores.mockImplementationOnce(() => new Promise(resolve => { resolveHistorical = resolve; }));

    await userEvent.click(screen.getByRole('button', { name: /previous/i }));

    // The whole page (including the Previous button) swaps to the skeleton while the new week's
    // fetch is pending — same as first load, not a frozen view of week 3's now-stale content.
    await waitFor(() => expect(screen.queryByRole('button', { name: /previous/i })).toBeNull());
    expect(document.querySelectorAll('.MuiSkeleton-root').length).toBeGreaterThan(0);

    resolveHistorical(makeScores(2, false, true));

    await waitFor(() => expect(screen.getByRole('button', { name: /previous/i })).toBeInTheDocument());
    expect(document.querySelectorAll('.MuiSkeleton-root').length).toBe(0);
  });

  // frizat: regression caught by /code-review on the isPlaceholderData fix above — a query with
  // enabled:false never resolves out of "placeholder" state, so once currentLeague goes from a
  // real value to null (e.g. the user gets removed from their only league) after a successful
  // load, isPlaceholderData stays permanently true and the page would get stuck on the skeleton
  // forever instead of falling through to the "select a league" screen.
  it('falls through to the no-league message, not a stuck skeleton, when currentLeague goes from set to null after a successful load', async () => {
    await setupDefaults();
    const { rerender, queryClient } = renderWithClient(<ScoresPage adapter={createNflAdapter()} />);
    await screen.findByText(/Scores/i);
    await waitFor(() => expect(screen.getAllByText(/BUF/i).length).toBeGreaterThan(0));

    sessionState.currentLeague = null;
    rerender(
      <QueryClientProvider client={queryClient}>
        <ScoresPage adapter={createNflAdapter()} />
      </QueryClientProvider>,
    );

    await waitFor(() => expect(screen.getByText(/Please select a league/i)).toBeInTheDocument());
    expect(document.querySelectorAll('.MuiSkeleton-root').length).toBe(0);
  });
});
