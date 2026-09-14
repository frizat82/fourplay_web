import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { vi } from 'vitest';
import PicksIsland from '../components/PicksIsland';
import type { SportAdapter, LoadedWeek } from '../services/sportAdapter';
import type { LeagueUserMappingDto } from '../types/league';
import { mockLeagueJuiceEmpty } from '../test/fixtures';

const sessionState = {
  availableLeagues: [] as LeagueUserMappingDto[],
  leaguesLoaded: true,
};

vi.mock('../services/session', () => ({ useSession: () => sessionState }));
vi.mock('../services/auth', () => ({ useAuth: () => ({ user: { userId: 'user-1', name: 'Alice', claims: [] } }) }));

vi.mock('../api/league', () => ({ getLeagueJuice: vi.fn() }));
import { getLeagueJuice } from '../api/league';
const mockedGetLeagueJuice = vi.mocked(getLeagueJuice);

function makeLeague(leagueId: number, leagueName: string): LeagueUserMappingDto {
  return { id: leagueId, leagueId, userId: 'user-1', leagueName, leagueType: 0, dateCreated: '' };
}

function makeLoadedWeek(overrides: Partial<LoadedWeek> = {}): LoadedWeek {
  return {
    season: 2025, week: 3, isPostSeason: false,
    games: [], userPicks: [], hasOdds: true, requiredPicks: 4,
    maxWeek: 18, maxSeason: 2025,
    ...overrides,
  };
}

function makeAdapter(loadCurrentGames: SportAdapter['loadCurrentGames']): SportAdapter {
  return {
    sport: 'nfl',
    pollIntervalMs: 0,
    weekSelectorConfig: { maxRegularSeasonWeek: 18, minSeason: 2020 },
    loadCurrentGames,
  } as unknown as SportAdapter;
}

const renderWithClient = (adapter: SportAdapter) => {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: Infinity } } });
  return render(
    <QueryClientProvider client={client}>
      <PicksIsland adapter={adapter} />
    </QueryClientProvider>,
  );
};

describe('PicksIsland', () => {
  beforeEach(() => {
    sessionState.availableLeagues = [];
    sessionState.leaguesLoaded = true;
    mockedGetLeagueJuice.mockReset();
    mockLeagueJuiceEmpty(mockedGetLeagueJuice);
  });

  it('renders nothing when the user has no leagues', () => {
    sessionState.availableLeagues = [];
    const { container } = renderWithClient(makeAdapter(vi.fn()));
    expect(container.firstChild).toBeNull();
  });

  it('shows no picks yet with the required count when the league has none', async () => {
    sessionState.availableLeagues = [makeLeague(1, 'Demo League')];
    const adapter = makeAdapter(vi.fn().mockResolvedValue(makeLoadedWeek({ userPicks: [] })));
    renderWithClient(adapter);

    // "Demo League" renders in every state (including the initial "Loading…" one), so waiting on
    // it alone isn't enough to know the query has resolved — wait on the actual final content.
    await screen.findByText(/no picks yet — 4 needed/i);
  });

  it('shows picked team chips and a remaining-count chip', async () => {
    sessionState.availableLeagues = [makeLeague(1, 'Demo League')];
    const adapter = makeAdapter(vi.fn().mockResolvedValue(makeLoadedWeek({
      userPicks: [
        { gameId: 'g1', team: 'BUF', pickType: 'Spread', userId: 'user-1', userName: 'Alice' },
        { gameId: 'g2', team: 'DAL', pickType: 'Spread', userId: 'user-1', userName: 'Alice' },
      ],
    })));
    renderWithClient(adapter);

    await screen.findByText('BUF');
    expect(screen.getByText('DAL')).toBeInTheDocument();
    expect(screen.getByText(/2 more needed/i)).toBeInTheDocument();
  });

  it('labels Over/Under picks distinctly from a plain spread pick on the same team', async () => {
    sessionState.availableLeagues = [makeLeague(1, 'Demo League')];
    const adapter = makeAdapter(vi.fn().mockResolvedValue(makeLoadedWeek({
      requiredPicks: 1,
      userPicks: [{ gameId: 'g1', team: 'BUF', pickType: 'Over', userId: 'user-1', userName: 'Alice' }],
    })));
    renderWithClient(adapter);

    await screen.findByText('BUF O');
  });

  it('shows a not-released-yet message instead of picks when odds are not posted', async () => {
    sessionState.availableLeagues = [makeLeague(1, 'Demo League')];
    const adapter = makeAdapter(vi.fn().mockResolvedValue(makeLoadedWeek({ hasOdds: false })));
    renderWithClient(adapter);

    await screen.findByText(/spreads not released yet/i);
  });

  it('shows one summary per league when the user is in multiple', async () => {
    sessionState.availableLeagues = [makeLeague(1, 'Demo League'), makeLeague(2, 'Friends League')];
    const adapter = makeAdapter(vi.fn().mockResolvedValue(makeLoadedWeek({ userPicks: [] })));
    renderWithClient(adapter);

    await screen.findByText('Demo League');
    expect(screen.getByText('Friends League')).toBeInTheDocument();
  });
});
