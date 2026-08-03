import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { vi } from 'vitest';
import DashboardStandings from '../components/DashboardStandings';
import type { SportAdapter } from '../services/sportAdapter';
import type { LeagueUserMappingDto } from '../types/league';
import type { LeaderboardDto } from '../types/leaderboard';

const sessionState = {
  availableLeagues: [] as LeagueUserMappingDto[],
  leaguesLoaded: true,
};

vi.mock('../services/session', () => ({ useSession: () => sessionState }));
vi.mock('../services/auth', () => ({ useAuth: () => ({ user: { userId: 'user-1', name: 'Alice', claims: [] } }) }));

vi.mock('../api/leaderboard', () => ({ getLeaderboard: vi.fn() }));
import { getLeaderboard } from '../api/leaderboard';
const mockedGetLeaderboard = vi.mocked(getLeaderboard);

function makeAdapter(seasonYear = 2025): SportAdapter {
  return {
    sport: 'nfl',
    pollIntervalMs: 0,
    weekSelectorConfig: { maxRegularSeasonWeek: 18, minSeason: 2020, weekLabelFn: () => '' },
    currentSeasonYear: async () => seasonYear,
  } as unknown as SportAdapter;
}

function makeLeague(leagueId: number, leagueName: string): LeagueUserMappingDto {
  return { id: leagueId, leagueId, userId: 'user-1', leagueName, leagueType: 0, dateCreated: '' };
}

function makeRow(userId: string, rank: string, total: number): LeaderboardDto {
  return { userId, userName: userId, rank, total, weekResults: [] };
}

const renderWithClient = () => {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: Infinity } } });
  return render(
    <QueryClientProvider client={client}>
      <DashboardStandings adapter={makeAdapter()} />
    </QueryClientProvider>,
  );
};

describe('DashboardStandings', () => {
  beforeEach(() => {
    mockedGetLeaderboard.mockReset();
    sessionState.availableLeagues = [];
    sessionState.leaguesLoaded = true;
  });

  it('renders nothing when the user has no leagues', async () => {
    sessionState.availableLeagues = [];
    const { container } = renderWithClient();
    await waitFor(() => expect(container).toBeEmptyDOMElement());
  });

  it('shows per-league rank and total, plus a summed grand total', async () => {
    sessionState.availableLeagues = [makeLeague(1, 'Demo League'), makeLeague(2, 'Friends League')];
    mockedGetLeaderboard.mockImplementation(async (leagueId) =>
      leagueId === 1
        ? [makeRow('user-1', '1', 150), makeRow('user-2', '2', -150)]
        : [makeRow('user-1', '4', -30), makeRow('user-2', '1', 30)],
    );

    renderWithClient();

    await screen.findByText('Demo League');
    expect(screen.getByText('Friends League')).toBeInTheDocument();
    expect(screen.getByText(/rank 1/i)).toBeInTheDocument();
    expect(screen.getByText(/rank 4/i)).toBeInTheDocument();
    expect(screen.getByText('+150')).toBeInTheDocument();
    expect(screen.getByText('-30')).toBeInTheDocument();
    // Grand total: 150 + (-30) = 120
    expect(screen.getByText('+120')).toBeInTheDocument();
  });

  it('skips a league where the user has no leaderboard row yet, without crashing', async () => {
    sessionState.availableLeagues = [makeLeague(1, 'Demo League'), makeLeague(2, 'Brand New League')];
    mockedGetLeaderboard.mockImplementation(async (leagueId) =>
      leagueId === 1 ? [makeRow('user-1', '1', 50)] : [],
    );

    renderWithClient();

    await screen.findByText('Demo League');
    expect(screen.queryByText('Brand New League')).not.toBeInTheDocument();
    expect(screen.getByText('+50')).toBeInTheDocument();
  });
});
