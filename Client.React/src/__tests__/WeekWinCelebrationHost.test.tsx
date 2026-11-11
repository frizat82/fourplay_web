import { describe, it, expect, vi, beforeAll, beforeEach } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import WeekWinCelebrationHost from '../components/WeekWinCelebrationHost';
import type { LoadedScores, SportAdapter } from '../services/sportAdapter';
import type { LeagueUserMappingDto } from '../types/league';
import type { LeaderboardDto, WeekResult } from '../types/leaderboard';
import { createGameView, createLeaderboardEntry, createLeaderboardWeekResult, createPickView } from '../test/fixtures';
import { hasSeenWeekWin } from '../utils/weekWinSeen';

const sessionState: { availableLeagues: LeagueUserMappingDto[]; leaguesLoaded: boolean } = { availableLeagues: [], leaguesLoaded: true };
vi.mock('../services/session', () => ({ useSession: () => sessionState }));
vi.mock('../services/auth', () => ({ useAuth: () => ({ user: { userId: 'me', name: 'Me', claims: [] } }) }));
vi.mock('../components/sports/TeamArt', () => ({
  default: ({ abbr }: { abbr: string }) => <div data-testid={`team-art-${abbr}`}>{abbr}</div>,
}));
vi.mock('../api/leaderboard', () => ({ getLeaderboard: vi.fn() }));
import { getLeaderboard } from '../api/leaderboard';
const mockedGetLeaderboard = vi.mocked(getLeaderboard);

const SEASON = 2026;

function league(leagueId: number, leagueName: string): LeagueUserMappingDto {
  return { id: leagueId, leagueId, userId: 'me', leagueName, leagueType: 0, dateCreated: '' };
}

/** The viewer and one opponent over two weeks; the opponent's results mirror the viewer's. */
function board(mine: [WeekResult, number][]): LeaderboardDto[] {
  const row = (userId: string, weeks: [WeekResult, number][]) => createLeaderboardEntry({
    userId,
    weekResults: weeks.map(([weekResult, score], i) => createLeaderboardWeekResult({ week: i + 1, weekResult, score })),
  });
  return [row('me', mine), row('bob', mine.map(([r, s]) => [r === 'Won' ? 'Lost' : 'Won', -s]))];
}

const weekScores: LoadedScores = {
  season: SEASON, week: 2, isPostSeason: false, hasOdds: true, hasActiveGames: false, requiredPicks: 1, weekLabel: 'Week 2',
  games: [createGameView({ homeTeam: 'DAL', awayTeam: 'NYG', homeSpread: -6.5, awaySpread: 6.5, gameStatus: 'final', homeScore: 34, awayScore: 17 })],
  allPicks: [],
  userPicks: [createPickView('me', 'DAL', 'Spread', 'me')],
};

const loadHistoricalScores = vi.fn<SportAdapter['loadHistoricalScores']>();
const adapter = {
  sport: 'nfl',
  currentSeasonYear: async () => SEASON,
  loadHistoricalScores,
} as unknown as SportAdapter;

function renderHost() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: Infinity } } });
  return render(
    <QueryClientProvider client={client}>
      <WeekWinCelebrationHost adapter={adapter} />
    </QueryClientProvider>,
  );
}

describe('WeekWinCelebrationHost', () => {
  // The host loads the animation lazily. Load it up front so the first test isn't timing a cold import.
  beforeAll(async () => { await import('../components/WeekWinCelebration'); });

  beforeEach(() => {
    localStorage.clear();
    mockedGetLeaderboard.mockReset();
    loadHistoricalScores.mockReset().mockResolvedValue(weekScores);
    sessionState.availableLeagues = [league(7, 'Sunday Sweats')];
    sessionState.leaguesLoaded = true;
  });

  it('celebrates the latest final week when the viewer won it, with their picks and winnings', async () => {
    mockedGetLeaderboard.mockResolvedValue(board([['Lost', -10], ['Won', 20]]));
    renderHost();
    const dialog = await screen.findByRole('dialog', { name: 'You won the week!' });
    expect(dialog).toHaveTextContent('Sunday Sweats');
    expect(dialog).toHaveTextContent('Week 2');
    expect(screen.getByLabelText('You collect $20')).toBeInTheDocument();
    expect(screen.getByRole('img', { name: 'DAL covered by 10.5' })).toBeInTheDocument();
    expect(loadHistoricalScores).toHaveBeenCalledWith(7, 'me', expect.objectContaining({ season: SEASON, week: 2 }));
  });

  it('plays once: a tap closes it, and it does not come back on the next visit', async () => {
    mockedGetLeaderboard.mockResolvedValue(board([['Lost', -10], ['Won', 20]]));
    const first = renderHost();
    fireEvent.click(await screen.findByText('You won the week!'));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(hasSeenWeekWin('me', 7, SEASON, 2)).toBe(true);
    first.unmount();

    renderHost();
    await waitFor(() => expect(mockedGetLeaderboard).toHaveBeenCalledTimes(2));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('stays quiet, and loads nothing extra, when the viewer lost the latest week', async () => {
    mockedGetLeaderboard.mockResolvedValue(board([['Won', 10], ['Lost', -20]]));
    renderHost();
    await waitFor(() => expect(mockedGetLeaderboard).toHaveBeenCalled());
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(loadHistoricalScores).not.toHaveBeenCalled();
  });

  it("still celebrates, without picks, when the week's games can't be loaded", async () => {
    mockedGetLeaderboard.mockResolvedValue(board([['Lost', -10], ['Won', 20]]));
    loadHistoricalScores.mockRejectedValue(new Error('offline'));
    renderHost();
    const dialog = await screen.findByRole('dialog', { name: 'You won the week!' });
    expect(dialog).toHaveTextContent('Week 2');
    expect(screen.getByLabelText('You collect $20')).toBeInTheDocument();
    expect(screen.queryByRole('img', { name: /covered/ })).not.toBeInTheDocument();
  });

  it('falls back the same way when the week has no stored games', async () => {
    mockedGetLeaderboard.mockResolvedValue(board([['Lost', -10], ['Won', 20]]));
    loadHistoricalScores.mockResolvedValue(null);
    renderHost();
    expect(await screen.findByRole('dialog', { name: 'You won the week!' })).toHaveTextContent('Week 2');
  });

  it('plays one league after another when the viewer won in two', async () => {
    sessionState.availableLeagues = [league(7, 'Sunday Sweats'), league(8, 'Office Pool')];
    mockedGetLeaderboard.mockResolvedValue(board([['Lost', -10], ['Won', 20]]));
    renderHost();
    expect(await screen.findByRole('dialog')).toHaveTextContent('Sunday Sweats');
    fireEvent.click(screen.getByText('You won the week!'));
    await waitFor(() => expect(screen.getByRole('dialog')).toHaveTextContent('Office Pool'));
    fireEvent.click(screen.getByText('You won the week!'));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });
});
