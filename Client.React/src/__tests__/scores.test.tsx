import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import ScoresPage from '../pages/ScoresPage';
import { createPick, createScores, createSpreadCalculationResponse } from '../test/fixtures';
import { vi } from 'vitest';
import type { NflPickDto } from '../types/picks';

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

vi.mock('../api/espn', () => ({ getScores: vi.fn(), loadScoresWithRetry: vi.fn(), getWeekScores: vi.fn() }));
vi.mock('../api/league', () => ({
  calculateSpreadBatch: vi.fn(),
  doOddsExist: vi.fn(),
  getLeaguePicks: vi.fn(),
}));
vi.mock('../services/spreadRelease', () => ({ getNextSpreadJob: vi.fn() }));
vi.mock('../utils/time', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../utils/time')>();
  return { ...actual, isPastNoonCst: vi.fn().mockReturnValue(false) };
});

import { getScores, loadScoresWithRetry, getWeekScores } from '../api/espn';
import { calculateSpreadBatch, doOddsExist, getLeaguePicks } from '../api/league';
import { getNextSpreadJob } from '../services/spreadRelease';

const mockedGetScores = vi.mocked(getScores);
const mockedLoadScoresWithRetry = vi.mocked(loadScoresWithRetry);
const mockedGetWeekScores = vi.mocked(getWeekScores);
const mockedDoOddsExist = vi.mocked(doOddsExist);
const mockedGetLeaguePicks = vi.mocked(getLeaguePicks);
const mockedCalculateSpreadBatch = vi.mocked(calculateSpreadBatch);
const mockedGetNextSpreadJob = vi.mocked(getNextSpreadJob);

const setupDefaults = async (options?: {
  week?: number;
  postSeason?: boolean;
  gameStarted?: boolean;
  oddsExist?: boolean;
  picks?: NflPickDto[];
}) => {
  const week = options?.week ?? 2;
  const postSeason = options?.postSeason ?? false;
  const gameStarted = options?.gameStarted ?? true;
  const scores = createScores({ week, postSeason, gameStarted });

  mockedGetScores.mockResolvedValue(scores);
  mockedLoadScoresWithRetry.mockResolvedValue(scores);
  mockedDoOddsExist.mockResolvedValue(options?.oddsExist ?? true);
  mockedGetLeaguePicks.mockResolvedValue(options?.picks ?? []);
  mockedGetNextSpreadJob.mockResolvedValue(null);
  mockedCalculateSpreadBatch.mockResolvedValue({
    results: {
      BUF: createSpreadCalculationResponse('BUF', -7, true, 47.5, 47.5),
      MIA: createSpreadCalculationResponse('MIA', 7, false, 47.5, 47.5),
      DAL: createSpreadCalculationResponse('DAL', -3.5, true, 44, 44),
      NYG: createSpreadCalculationResponse('NYG', 3.5, false, 44, 44),
    },
  });
};

const renderPage = async () => {
  const utils = render(<ScoresPage />);
  await screen.findByText(/Scores/i);
  // Wait for loading state to finish — the loading header shows "Scores" too,
  // so we must also wait for the spinner to disappear before asserting on content.
  await waitFor(() => expect(screen.queryByRole('progressbar')).toBeNull());
  return utils;
};

describe('ScoresPage', () => {
  beforeEach(() => {
    sessionState.currentLeague = 1;
    mockedGetScores.mockReset();
    mockedLoadScoresWithRetry.mockReset();
    mockedGetWeekScores.mockReset();
    mockedDoOddsExist.mockReset();
    mockedGetLeaguePicks.mockReset();
    mockedCalculateSpreadBatch.mockReset();
    mockedGetNextSpreadJob.mockReset();
  });

  it('shows no league message when no league selected', async () => {
    sessionState.currentLeague = null;
    await setupDefaults();
    render(<ScoresPage />);
    await screen.findByText(/Please select a league/i);
  });

  it('shows odds not posted when odds missing', async () => {
    await setupDefaults({ oddsExist: false });
    render(<ScoresPage />);
    await screen.findByText(/Odds Not Posted/i);
  });

  it('shows week title when scores available', async () => {
    await setupDefaults({ week: 5 });
    await renderPage();
    expect(screen.getAllByText(/Week 5/i).length).toBeGreaterThan(0);
  });

  it('shows postseason wild card title', async () => {
    await setupDefaults({ week: 1, postSeason: true });
    await renderPage();
    expect(screen.getAllByText(/Wild Card/i).length).toBeGreaterThan(0);
  });

  it('displays team logos and scores when game completed', async () => {
    await setupDefaults({ gameStarted: true });
    await renderPage();

    const images = screen.getAllByRole('img');
    expect(images.length).toBeGreaterThanOrEqual(2);
    expect(screen.getByText('21')).toBeInTheDocument();
    expect(screen.getByText('14')).toBeInTheDocument();
  });

  it('shows pick dialog buttons when game started and picks exist', async () => {
    const picks = [createPick({ team: 'BUF' })];
    await setupDefaults({ picks, gameStarted: true });
    await renderPage();

    const personIcons = screen.getAllByTestId('PersonIcon');
    expect(personIcons.length).toBeGreaterThan(0);
  });

  it('disables pick buttons when game not started', async () => {
    const picks = [createPick({ team: 'BUF' })];
    await setupDefaults({ picks, gameStarted: false });
    await renderPage();

    const buttons = screen.getAllByRole('button');
    expect(buttons.some((btn) => btn.hasAttribute('disabled'))).toBe(true);
  });

  it('displays spread information when available', async () => {
    await setupDefaults();
    await renderPage();
    expect(screen.getAllByText(/BUF/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/MIA/i).length).toBeGreaterThan(0);
  });

  it('shows loading spinner while loading', async () => {
    mockedGetScores.mockImplementation(async () => createScores({ week: 2 }));
    mockedLoadScoresWithRetry.mockImplementation(async () => createScores({ week: 2 }));
    mockedDoOddsExist.mockResolvedValue(true);
    mockedGetLeaguePicks.mockResolvedValue([]);
    mockedCalculateSpreadBatch.mockResolvedValue({ results: {} });

    render(<ScoresPage />);
    expect(screen.getByRole('progressbar')).toBeInTheDocument();
    await screen.findByText(/Scores/i);
  });

  it('renders multiple games correctly', async () => {
    await setupDefaults();
    await renderPage();
    expect(screen.getAllByText(/BUF/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/MIA/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/DAL/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/NYG/i).length).toBeGreaterThan(0);
  });

  it('postseason displays over/under icons', async () => {
    await setupDefaults({ week: 1, postSeason: true });
    await renderPage();
    expect(screen.getAllByTestId('ArrowCircleUpIcon').length).toBeGreaterThan(0);
    expect(screen.getAllByTestId('ArrowCircleDownIcon').length).toBeGreaterThan(0);
  });

  it('applies badge color classes for winning and losing picks', async () => {
    const picks = [createPick({ team: 'BUF' }), createPick({ team: 'MIA' })];
    await setupDefaults({ picks, gameStarted: true });
    const { getByTestId } = await renderPage();
    // Current user (TestUser) has picks — badge is always info (blue) regardless of win/loss
    expect(getByTestId('badge-BUF-spread')).toHaveAttribute('data-tone', 'info');
    expect(getByTestId('badge-MIA-spread')).toHaveAttribute('data-tone', 'info');
  });

  it('postseason over badge shows count for home team picks', async () => {
    const picks = [
      createPick({ team: 'BUF', pick: 'Over' }),
      createPick({ team: 'BUF', pick: 'Over', userName: 'OtherUser', userId: '456' }),
    ];
    await setupDefaults({ week: 1, postSeason: true, picks, gameStarted: true });
    const { getByTestId } = await renderPage();
    const badge = getByTestId('badge-BUF-over');
    // badge should be visible (badgeContent=2, invisible=false)
    expect(badge.querySelector('.MuiBadge-badge')).toHaveTextContent('2');
  });

  it('postseason under badge shows count for home team picks', async () => {
    const picks = [createPick({ team: 'BUF', pick: 'Under' })];
    await setupDefaults({ week: 1, postSeason: true, picks, gameStarted: true });
    const { getByTestId } = await renderPage();
    const badge = getByTestId('badge-BUF-under');
    expect(badge.querySelector('.MuiBadge-badge')).toHaveTextContent('1');
  });

  it('spread badge is blue when current user has a pick', async () => {
    const picks = [
      createPick({ team: 'BUF' }),
      createPick({ team: 'BUF', userName: 'OtherUser', userId: '456' }),
    ];
    await setupDefaults({ picks, gameStarted: true });
    const { getByTestId } = await renderPage();
    // TestUser picked BUF — badge should be info (blue)
    expect(getByTestId('badge-BUF-spread')).toHaveAttribute('data-tone', 'info');
  });

  it('over badge is blue when current user has an over pick', async () => {
    const picks = [createPick({ team: 'BUF', pick: 'Over' })];
    await setupDefaults({ week: 1, postSeason: true, picks, gameStarted: true });
    const { getByTestId } = await renderPage();
    expect(getByTestId('badge-BUF-over')).toHaveAttribute('data-tone', 'info');
  });

  it('spread badge is green for other user winning pick', async () => {
    // OtherUser picked BUF (isWinner=true), TestUser did not
    const picks = [createPick({ team: 'BUF', userName: 'OtherUser', userId: '456' })];
    await setupDefaults({ picks, gameStarted: true });
    const { getByTestId } = await renderPage();
    expect(getByTestId('badge-BUF-spread')).toHaveAttribute('data-tone', 'success');
  });

  it('spread badge is red for other user losing pick', async () => {
    // OtherUser picked MIA (isWinner=false in mock), TestUser did not
    const picks = [createPick({ team: 'MIA', userName: 'OtherUser', userId: '456' })];
    await setupDefaults({ picks, gameStarted: true });
    const { getByTestId } = await renderPage();
    expect(getByTestId('badge-MIA-spread')).toHaveAttribute('data-tone', 'error');
  });

  it('spread badge is primary before game starts', async () => {
    const picks = [createPick({ team: 'BUF', userName: 'OtherUser', userId: '456' })];
    await setupDefaults({ picks, gameStarted: false });
    const { getByTestId } = await renderPage();
    expect(getByTestId('badge-BUF-spread')).toHaveAttribute('data-tone', 'primary');
  });

  it('under badge is blue when current user has under pick', async () => {
    const picks = [createPick({ team: 'BUF', pick: 'Under' })];
    await setupDefaults({ week: 1, postSeason: true, picks, gameStarted: true });
    const { getByTestId } = await renderPage();
    expect(getByTestId('badge-BUF-under')).toHaveAttribute('data-tone', 'info');
  });

  it('over badge is green for other user winning over pick', async () => {
    // OtherUser picked Over for BUF game — isOverWinner=true in mock
    const picks = [createPick({ team: 'BUF', pick: 'Over', userName: 'OtherUser', userId: '456' })];
    await setupDefaults({ week: 1, postSeason: true, picks, gameStarted: true });
    const { getByTestId } = await renderPage();
    expect(getByTestId('badge-BUF-over')).toHaveAttribute('data-tone', 'success');
  });

  it('over badge is red for other user losing over pick', async () => {
    // OtherUser picked Over for MIA game — need isOverWinner=false
    mockedCalculateSpreadBatch.mockResolvedValue({
      results: {
        BUF: createSpreadCalculationResponse('BUF', -7, true, 47.5, 47.5, false, true),
        MIA: createSpreadCalculationResponse('MIA', 7, false, 47.5, 47.5, false, true),
        DAL: createSpreadCalculationResponse('DAL', -3.5, true, 44, 44),
        NYG: createSpreadCalculationResponse('NYG', 3.5, false, 44, 44),
      },
    });
    const picks = [createPick({ team: 'BUF', pick: 'Over', userName: 'OtherUser', userId: '456' })];
    const scores = createScores({ week: 1, postSeason: true, gameStarted: true });
    mockedGetScores.mockResolvedValue(scores);
    mockedLoadScoresWithRetry.mockResolvedValue(scores);
    mockedDoOddsExist.mockResolvedValue(true);
    mockedGetLeaguePicks.mockResolvedValue(picks);
    mockedGetNextSpreadJob.mockResolvedValue(null);
    const { getByTestId } = await renderPage();
    expect(getByTestId('badge-BUF-over')).toHaveAttribute('data-tone', 'error');
  });

  it('under badge is green for other user winning under pick', async () => {
    // OtherUser picked Under for BUF game — isUnderWinner=true in default mock
    const picks = [createPick({ team: 'BUF', pick: 'Under', userName: 'OtherUser', userId: '456' })];
    await setupDefaults({ week: 1, postSeason: true, picks, gameStarted: true });
    const { getByTestId } = await renderPage();
    expect(getByTestId('badge-BUF-under')).toHaveAttribute('data-tone', 'success');
  });

  it('under badge is red for other user losing under pick', async () => {
    // Override so BUF isUnderWinner=false
    mockedCalculateSpreadBatch.mockResolvedValue({
      results: {
        BUF: createSpreadCalculationResponse('BUF', -7, true, 47.5, 47.5, true, false),
        MIA: createSpreadCalculationResponse('MIA', 7, false, 47.5, 47.5),
        DAL: createSpreadCalculationResponse('DAL', -3.5, true, 44, 44),
        NYG: createSpreadCalculationResponse('NYG', 3.5, false, 44, 44),
      },
    });
    const picks = [createPick({ team: 'BUF', pick: 'Under', userName: 'OtherUser', userId: '456' })];
    const scores = createScores({ week: 1, postSeason: true, gameStarted: true });
    mockedGetScores.mockResolvedValue(scores);
    mockedLoadScoresWithRetry.mockResolvedValue(scores);
    mockedDoOddsExist.mockResolvedValue(true);
    mockedGetLeaguePicks.mockResolvedValue(picks);
    mockedGetNextSpreadJob.mockResolvedValue(null);
    const { getByTestId } = await renderPage();
    expect(getByTestId('badge-BUF-under')).toHaveAttribute('data-tone', 'error');
  });

  it('show only my picks hides game without user picks', async () => {
    // TestUser picked BUF (game 1), not DAL/NYG (game 2)
    const picks = [createPick({ team: 'BUF' })];
    await setupDefaults({ picks, gameStarted: true });
    await renderPage();
    await userEvent.click(screen.getByRole('button', { name: /show only my picks/i }));
    await waitFor(() => {
      expect(screen.queryByText('DAL')).not.toBeInTheDocument();
      expect(screen.queryByText('NYG')).not.toBeInTheDocument();
    });
    expect(screen.getAllByText(/BUF/i).length).toBeGreaterThan(0);
  });

  it('show only my picks shows game with user spread pick', async () => {
    const picks = [createPick({ team: 'DAL' }), createPick({ team: 'MIA', userName: 'OtherUser', userId: '456' })];
    await setupDefaults({ picks, gameStarted: true });
    await renderPage();
    await userEvent.click(screen.getByRole('button', { name: /show only my picks/i }));
    await waitFor(() => {
      // TestUser picked DAL — DAL game (DAL vs NYG) should show
      expect(screen.getAllByText(/DAL/i).length).toBeGreaterThan(0);
      // TestUser did NOT pick BUF or MIA — BUF/MIA game should be hidden
      expect(screen.queryByText('BUF')).not.toBeInTheDocument();
    });
  });

  it('show only my picks shows game when user made over pick', async () => {
    const picks = [createPick({ team: 'BUF', pick: 'Over' })];
    await setupDefaults({ week: 1, postSeason: true, picks, gameStarted: true });
    await renderPage();
    await userEvent.click(screen.getByRole('button', { name: /show only my picks/i }));
    await waitFor(() => {
      expect(screen.getAllByText(/BUF/i).length).toBeGreaterThan(0);
      expect(screen.queryByText('DAL')).not.toBeInTheDocument();
    });
  });

  it('show only my picks shows game when user made under pick', async () => {
    const picks = [createPick({ team: 'BUF', pick: 'Under' })];
    await setupDefaults({ week: 1, postSeason: true, picks, gameStarted: true });
    await renderPage();
    await userEvent.click(screen.getByRole('button', { name: /show only my picks/i }));
    await waitFor(() => {
      expect(screen.getAllByText(/BUF/i).length).toBeGreaterThan(0);
      expect(screen.queryByText('DAL')).not.toBeInTheDocument();
    });
  });

  it('badge is invisible before game starts', async () => {
    const picks = [createPick({ team: 'BUF' }), createPick({ team: 'BUF', userName: 'OtherUser', userId: '456' })];
    await setupDefaults({ picks, gameStarted: false });
    const { getByTestId } = await renderPage();
    const badge = getByTestId('badge-BUF-spread');
    // When game hasn't started and not past noon CST, badge is invisible (MUI adds invisible class)
    expect(badge.querySelector('.MuiBadge-badge')).toHaveClass('MuiBadge-invisible');
  });

  it('spread badge count matches number of pickers', async () => {
    const picks = [
      createPick({ team: 'BUF' }),
      createPick({ team: 'BUF', userName: 'User2', userId: '456' }),
      createPick({ team: 'BUF', userName: 'User3', userId: '789' }),
    ];
    await setupDefaults({ picks, gameStarted: true });
    const { getByTestId } = await renderPage();
    const badge = getByTestId('badge-BUF-spread');
    expect(badge.querySelector('.MuiBadge-badge')).toHaveTextContent('3');
  });

  it('loadHistoricalWeek calls doOddsExist with internal week not raw ESPN week', async () => {
    // oddsExist:true required so the full page renders and WeekYearSelector is visible
    await setupDefaults({ week: 1, postSeason: true, oddsExist: true });
    mockedCalculateSpreadBatch.mockResolvedValue({ results: {} });
    await renderPage();

    mockedGetWeekScores.mockResolvedValue(createScores({ week: 2, postSeason: true, gameStarted: true }));
    await userEvent.click(screen.getByRole('button', { name: /next/i }));

    await waitFor(() => {
      // ESPN week 2 postseason → internal week 20 (getWeekFromEspnWeek(2, true))
      expect(mockedDoOddsExist).toHaveBeenCalledWith(1, expect.any(Number), 20);
    });
  });

  it('toggles to matrix view', async () => {
    await setupDefaults();
    await renderPage();
    const toggle = screen.getByRole('button', { name: /show as matrix/i });
    await userEvent.click(toggle);
    await waitFor(() => {
      expect(screen.getByText(/Show Standard View/i)).toBeInTheDocument();
    });
  });
});
