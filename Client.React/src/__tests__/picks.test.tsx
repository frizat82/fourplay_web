import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import PicksPage from '../pages/PicksPage';
import { createCompetition, createPick, createScores, createSpreadResponse } from '../test/fixtures';
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

const toastState = {
  push: vi.fn(),
};

vi.mock('../services/session', () => ({ useSession: () => sessionState }));
vi.mock('../services/auth', () => ({ useAuth: () => authState }));
vi.mock('../services/toast', () => ({ useToast: () => toastState }));

vi.mock('../api/espn', () => ({ getScores: vi.fn(), loadScoresWithRetry: vi.fn() }));
vi.mock('../api/league', () => ({
  addPicks: vi.fn(),
  doOddsExist: vi.fn(),
  getUserPicks: vi.fn(),
  spreadBatch: vi.fn(),
}));
vi.mock('../api/jersey', () => ({ getAllJerseys: vi.fn() }));
vi.mock('../services/spreadRelease', () => ({ getNextSpreadJob: vi.fn() }));

import { getScores, loadScoresWithRetry } from '../api/espn';
import { addPicks, doOddsExist, getUserPicks, spreadBatch } from '../api/league';
import { getAllJerseys } from '../api/jersey';
import { getNextSpreadJob } from '../services/spreadRelease';

const mockedGetScores = vi.mocked(getScores);
const mockedLoadScoresWithRetry = vi.mocked(loadScoresWithRetry);
const mockedDoOddsExist = vi.mocked(doOddsExist);
const mockedGetUserPicks = vi.mocked(getUserPicks);
const mockedSpreadBatch = vi.mocked(spreadBatch);
const mockedAddPicks = vi.mocked(addPicks);
const mockedGetAllJerseys = vi.mocked(getAllJerseys);
const mockedGetNextSpreadJob = vi.mocked(getNextSpreadJob);

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
  mockedLoadScoresWithRetry.mockResolvedValue(scores);
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

const renderPage = async () => {
  render(<PicksPage />);
  await screen.findByText(/Picks/i);
  await waitFor(() => expect(screen.queryByRole('progressbar')).toBeNull());
};

describe('PicksPage', () => {
  beforeEach(() => {
    sessionState.currentLeague = 1;
    toastState.push.mockReset();
    mockedGetScores.mockReset();
    mockedLoadScoresWithRetry.mockReset();
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
    render(<PicksPage />);
    await screen.findByText(/Please select a league/i);
  });

  it('shows odds not posted when odds missing', async () => {
    await setupDefaults({ oddsExist: false });
    render(<PicksPage />);
    await screen.findByText(/Odds Not Posted/i);
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

    const pickButtons = screen.getAllByRole('button', { name: /^Pick$/i });
    await userEvent.click(pickButtons[0]);

    await waitFor(() => {
      const submit = screen.getByRole('button', { name: /submit pick/i });
      const clear = screen.getByRole('button', { name: /clear selected picks/i });
      expect(submit).not.toBeDisabled();
      expect(clear).not.toBeDisabled();
    });
  });

  it('pick button toggles to picked and back', async () => {
    await setupDefaults();
    await renderPage();

    const pickButton = screen.getAllByRole('button', { name: /^Pick$/i })[0];
    await userEvent.click(pickButton);
    await screen.findByRole('button', { name: /picked/i });

    const pickedButton = screen.getAllByRole('button', { name: /picked/i })[0];
    await userEvent.click(pickedButton);

    await waitFor(() => {
      expect(screen.getAllByRole('button', { name: /^Pick$/i }).length).toBeGreaterThan(0);
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

    const pickButtons = screen.getAllByRole('button', { name: /^Pick$/i });
    await userEvent.click(pickButtons[0]);
    await userEvent.click(pickButtons[1]);

    await waitFor(() => {
      const remaining = screen.queryAllByRole('button', { name: /^Pick$/i });
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

    const initialPicked = screen.getAllByRole('button', { name: /picked/i }).length;
    expect(initialPicked).toBe(2);

    const pickButton = screen.getAllByRole('button', { name: /^Pick$/i })[0];
    await userEvent.click(pickButton);

    await waitFor(() => {
      expect(screen.getAllByRole('button', { name: /picked/i }).length).toBe(3);
    });

    await userEvent.click(screen.getByRole('button', { name: /clear selected picks/i }));

    await waitFor(() => {
      expect(screen.getAllByRole('button', { name: /picked/i }).length).toBe(2);
    });
  });

  it('submit picks clears user picks and reloads existing', async () => {
    await setupDefaults();
    mockedAddPicks.mockResolvedValue(1);
    mockedGetUserPicks.mockResolvedValue([createPick({ team: 'BUF' })]);

    await renderPage();
    const pickButton = screen.getAllByRole('button', { name: /^Pick$/i })[0];
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

    const pickButton = screen.getAllByRole('button', { name: /^Pick$/i })[0];
    expect(pickButton).not.toBeDisabled();
  });

  it('pick buttons disabled when game kickoff time has passed (even if ESPN status still scheduled)', async () => {
    const pastDate = new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString();
    await setupDefaults({ gameDate: pastDate, gameStarted: false });
    await renderPage();

    screen.getAllByRole('button', { name: /^Pick$/i }).forEach((btn) => {
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
    expect(screen.getAllByText(/Super Bowl/i).length).toBeGreaterThan(0);
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
    await screen.findByRole('button', { name: /^Overed$/i });

    const underButton = screen.getAllByRole('button', { name: /^Under$/i })[0];
    await userEvent.click(underButton);
    await screen.findByRole('button', { name: /^Undered$/i });
  });

  it('postseason allows selecting spread and over picks together', async () => {
    await setupDefaults({ week: 1, postSeason: true });
    await renderPage();

    const pickButton = screen.getAllByRole('button', { name: /^Pick$/i })[0];
    await userEvent.click(pickButton);
    await screen.findByRole('button', { name: /picked/i });

    const overButton = screen.getAllByRole('button', { name: /^Over$/i })[0];
    await userEvent.click(overButton);
    await screen.findByRole('button', { name: /^Overed$/i });
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
});
