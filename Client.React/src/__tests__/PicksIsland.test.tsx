import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { vi } from 'vitest';
import PicksIsland from '../components/PicksIsland';
import type { SportAdapter, LoadedWeek, GameView } from '../services/sportAdapter';
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

const renderWithClient = (adapter: SportAdapter, client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: Infinity } } })) => {
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

  // Win/loss + locked indicators (frizat: reuses GameView.homeCovers/awayCovers/overWins/
  // underWins — already computed by the adapter via gameHelpers.ts's shared cover functions —
  // and gameHelpers.ts's isGameLocked, rather than recomputing anything here).
  function makeGame(overrides: Partial<GameView> = {}): GameView {
    return {
      id: 'BUFvsMIA', homeTeam: 'BUF', awayTeam: 'MIA',
      homeSpread: -3, awaySpread: 3, overThreshold: 45, underThreshold: 45,
      homeScore: null, awayScore: null,
      gameStatus: 'scheduled', gameTime: new Date(Date.now() + 3600_000).toISOString(),
      ...overrides,
    };
  }

  it('shows a check icon on a picked team once its game is won', async () => {
    sessionState.availableLeagues = [makeLeague(1, 'Demo League')];
    const adapter = makeAdapter(vi.fn().mockResolvedValue(makeLoadedWeek({
      games: [makeGame({ gameStatus: 'final', homeCovers: true, awayCovers: false })],
      userPicks: [{ gameId: 'BUFvsMIA', team: 'BUF', pickType: 'Spread', userId: 'user-1', userName: 'Alice' }],
    })));
    const { container } = renderWithClient(adapter);

    await screen.findByText('BUF');
    expect(container.querySelector('[data-testid="CheckIcon"]')).toBeInTheDocument();
    expect(container.querySelector('.MuiChip-colorSuccess')).toBeInTheDocument();
  });

  it('shows a close icon on a picked team once its game is lost', async () => {
    sessionState.availableLeagues = [makeLeague(1, 'Demo League')];
    const adapter = makeAdapter(vi.fn().mockResolvedValue(makeLoadedWeek({
      games: [makeGame({ gameStatus: 'final', homeCovers: false, awayCovers: true })],
      userPicks: [{ gameId: 'BUFvsMIA', team: 'BUF', pickType: 'Spread', userId: 'user-1', userName: 'Alice' }],
    })));
    const { container } = renderWithClient(adapter);

    await screen.findByText('BUF');
    expect(container.querySelector('[data-testid="CloseIcon"]')).toBeInTheDocument();
    expect(container.querySelector('.MuiChip-colorError')).toBeInTheDocument();
  });

  it('shows a lock icon, not a win/loss icon, on a picked team once kicked off but not yet decided', async () => {
    sessionState.availableLeagues = [makeLeague(1, 'Demo League')];
    const adapter = makeAdapter(vi.fn().mockResolvedValue(makeLoadedWeek({
      // Kickoff already passed (gameTime in the past) but ESPN's status hasn't caught up yet —
      // the exact gap isGameLocked's time-based fallback exists for.
      games: [makeGame({ gameStatus: 'scheduled', gameTime: new Date(Date.now() - 3600_000).toISOString() })],
      userPicks: [{ gameId: 'BUFvsMIA', team: 'BUF', pickType: 'Spread', userId: 'user-1', userName: 'Alice' }],
    })));
    const { container } = renderWithClient(adapter);

    await screen.findByText('BUF');
    expect(container.querySelector('[data-testid="LockIcon"]')).toBeInTheDocument();
    expect(container.querySelector('[data-testid="CheckIcon"]')).not.toBeInTheDocument();
    expect(container.querySelector('[data-testid="CloseIcon"]')).not.toBeInTheDocument();
  });

  it('shows no icon on a picked team whose game has not started yet', async () => {
    sessionState.availableLeagues = [makeLeague(1, 'Demo League')];
    const adapter = makeAdapter(vi.fn().mockResolvedValue(makeLoadedWeek({
      games: [makeGame()],
      userPicks: [{ gameId: 'BUFvsMIA', team: 'BUF', pickType: 'Spread', userId: 'user-1', userName: 'Alice' }],
    })));
    const { container } = renderWithClient(adapter);

    await screen.findByText('BUF');
    expect(container.querySelector('[data-testid="LockIcon"]')).not.toBeInTheDocument();
    expect(container.querySelector('[data-testid="CheckIcon"]')).not.toBeInTheDocument();
    expect(container.querySelector('[data-testid="CloseIcon"]')).not.toBeInTheDocument();
  });

  it('resolves an Over/Under pick result from the game it references, not a plain spread cover', async () => {
    sessionState.availableLeagues = [makeLeague(1, 'Demo League')];
    const adapter = makeAdapter(vi.fn().mockResolvedValue(makeLoadedWeek({
      requiredPicks: 1,
      games: [makeGame({ gameStatus: 'final', homeCovers: false, awayCovers: true, overWins: true, underWins: false })],
      userPicks: [{ gameId: 'BUFvsMIA', team: 'BUF', pickType: 'Over', userId: 'user-1', userName: 'Alice' }],
    })));
    const { container } = renderWithClient(adapter);

    await screen.findByText('BUF O');
    expect(container.querySelector('[data-testid="CheckIcon"]')).toBeInTheDocument();
  });

  // PicksPage renders the island only after its own live query (same key) has resolved, so the
  // island mounts onto fresh data. Refetching it there doubled every Picks cold load — the whole
  // current-week chain (week → scores/picks/odds → spreads) ran a second time. PicksPage's own
  // poll/SSE/focus refetches keep the shared entry live; the island only reads it.
  it('does not refetch current-week data that was just loaded under the shared key', async () => {
    sessionState.availableLeagues = [makeLeague(1, 'Demo League')];
    const loadCurrentGames = vi.fn().mockResolvedValue(makeLoadedWeek({ userPicks: [] }));
    const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: Infinity } } });
    client.setQueryData(['nfl', 'picks', 1, 'user-1', null], makeLoadedWeek({ userPicks: [] }));

    renderWithClient(makeAdapter(loadCurrentGames), client);

    await screen.findByText(/no picks yet — 4 needed/i);
    expect(loadCurrentGames).not.toHaveBeenCalled();
  });
});
