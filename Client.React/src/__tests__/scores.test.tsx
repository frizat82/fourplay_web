import { render, screen, waitFor } from '@testing-library/react';
import ScoresPage from '../pages/ScoresPage';
import { createNflAdapter } from '../services/nflAdapter';
import { createPick, createScores, createSpreadResponse, createCompetition } from '../test/fixtures';
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

vi.mock('../services/session', () => ({ useSession: () => sessionState }));
vi.mock('../services/auth', () => ({ useAuth: () => ({ user: { userId: '123', name: 'TestUser', claims: [] } }) }));
vi.mock('../api/espn', () => ({ getScores: vi.fn(), loadScoresWithRetry: vi.fn(), getWeekScores: vi.fn(), getLiveGames: vi.fn() }));
vi.mock('../api/league', () => ({
  doOddsExist: vi.fn(), getLeaguePicks: vi.fn(), spreadBatch: vi.fn(),
  addPicks: vi.fn(), getUserPicks: vi.fn(),
}));
vi.mock('../services/spreadRelease', () => ({ getNextSpreadJob: vi.fn().mockResolvedValue(null) }));

import { loadScoresWithRetry, getLiveGames } from '../api/espn';
import { doOddsExist, getLeaguePicks, spreadBatch } from '../api/league';

const mockedLoadScoresWithRetry = vi.mocked(loadScoresWithRetry);
const mockedGetLiveGames = vi.mocked(getLiveGames);
const mockedDoOddsExist = vi.mocked(doOddsExist);
const mockedGetLeaguePicks = vi.mocked(getLeaguePicks);
const mockedSpreadBatch = vi.mocked(spreadBatch);

// BUF home (24-10), spread -7: homeCovers = 24-7=17 > 10 ✓ (BUF covers → green)
// MIA away: !homeCovers → red
// Over at 47.5: 24+10=34 < 47.5 → Under wins (not over)
const SPREAD_RESPONSES = {
  BUF: createSpreadResponse(true, '-7', 47.5, 47.5),
  MIA: createSpreadResponse(false, '+7', 47.5, 47.5),
  DAL: createSpreadResponse(true, '-3', 47.5, 47.5),
  NYG: createSpreadResponse(false, '+3', 47.5, 47.5),
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
  mockedLoadScoresWithRetry.mockResolvedValue(makeScores(week, postSeason, gameStarted));
  mockedGetLiveGames.mockResolvedValue([]);
  mockedDoOddsExist.mockResolvedValue(options?.oddsExist ?? true);
  mockedGetLeaguePicks.mockResolvedValue(options?.picks ?? []);
  mockedSpreadBatch.mockResolvedValue({ responses: SPREAD_RESPONSES });
};

const renderPage = async () => {
  const utils = render(<ScoresPage adapter={createNflAdapter()} />);
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
    render(<ScoresPage adapter={createNflAdapter()} />);
    await screen.findByText(/Please select a league/i);
  });

  it('shows odds not posted when odds missing', async () => {
    await setupDefaults({ oddsExist: false });
    render(<ScoresPage adapter={createNflAdapter()} />);
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
});
