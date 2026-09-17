import { render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { vi } from 'vitest';
import { useSportContext } from '../services/sport';
import HomePage from '../pages/HomePage';
import type { UserInfo } from '../types/auth';
import type { SportAdapter } from '../services/sportAdapter';
import type { LeagueInfoDto } from '../types/admin';
import type { LeagueUserMappingDto } from '../types/league';

vi.mock('../services/sport', () => ({
  useSportContext: vi.fn(() => ({ sport: 'NFL', isCfb: false, isNfl: true })),
}));

// Matches the established authState idiom used across the suite (picks.test.tsx, scores.test.tsx,
// etc.) — a plain mutable object the mock closes over, mutated per-test — rather than a vi.fn()
// spy with a full AuthContextValue shape most tests here never read.
const authState = { user: null as UserInfo | null };
vi.mock('../services/auth', () => ({ useAuth: () => authState }));

// OwnerCostSummary/PicksIsland/DashboardStandings (rendered whenever isAuthed) all read
// useSession — empty/not-loaded by default so they stay no-ops for tests that don't care about
// them; the frizat-7c4 describe block below opts specific tests into real leagues.
const sessionState = {
  ownedLeagues: [] as LeagueInfoDto[],
  availableLeagues: [] as LeagueUserMappingDto[],
  leaguesLoaded: false,
};
vi.mock('../services/session', () => ({ useSession: () => sessionState }));

vi.mock('../api/league', () => ({ getLeagueJuice: vi.fn(), getLeagueCost: vi.fn() }));
vi.mock('../api/leaderboard', () => ({ getLeaderboard: vi.fn() }));
import { getLeagueJuice, getLeagueCost } from '../api/league';
import { getLeaderboard } from '../api/leaderboard';
const mockedGetLeagueJuice = vi.mocked(getLeagueJuice);
const mockedGetLeagueCost = vi.mocked(getLeagueCost);
const mockedGetLeaderboard = vi.mocked(getLeaderboard);

beforeEach(() => {
  authState.user = null;
  sessionState.ownedLeagues = [];
  sessionState.availableLeagues = [];
  sessionState.leaguesLoaded = false;
  mockedGetLeagueJuice.mockReset();
  mockedGetLeagueCost.mockReset();
  mockedGetLeaderboard.mockReset();
});

function renderPage(adapter?: SportAdapter) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <HomePage adapter={adapter} />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('HomePage — sport indicator', () => {
  // frizat: unauthenticated visitors had no way to tell which sport a given subdomain (ivleague
  // vs cfb.ivleague) was for — the hero copy was hardcoded to NFL regardless of hostname.
  it('shows an "NFL" badge on the NFL site', () => {
    vi.mocked(useSportContext).mockReturnValue({ sport: 'NFL', isCfb: false, isNfl: true });
    renderPage();
    expect(screen.getByText('NFL')).toBeInTheDocument();
    expect(screen.queryByText('College Football')).not.toBeInTheDocument();
  });

  it('shows a "College Football" badge on the CFB site', () => {
    vi.mocked(useSportContext).mockReturnValue({ sport: 'CFB', isCfb: true, isNfl: false });
    renderPage();
    expect(screen.getByText('College Football')).toBeInTheDocument();
    expect(screen.queryByText(/^NFL$/)).not.toBeInTheDocument();
  });
});

describe('HomePage — unauthenticated navigation', () => {
  // frizat: registration always requires a real invite code/link a commissioner sent — a bare
  // /account/register link (no invite params attached) was a guaranteed dead end for any
  // visitor without one already in hand. Only "Login" remains as a generic account action.
  it('has no generic Register link, only Login', () => {
    renderPage();
    expect(screen.queryByRole('link', { name: /^register$/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /register with invite/i })).not.toBeInTheDocument();
    expect(screen.getAllByRole('link', { name: /login/i }).length).toBeGreaterThan(0);
  });
});

describe('HomePage — authenticated hero CTA', () => {
  // frizat-ccm: the authenticated "Make Picks" button used SportsTennisIcon — a literal tennis
  // racket, thematically wrong for a football pick'em app on its primary post-login CTA.
  it('shows a football icon, not a tennis racket, on the "Make Picks" button', () => {
    authState.user = { userId: '1', name: 'Alice', claims: [] };
    renderPage();
    const makePicksButton = screen.getByRole('link', { name: /make picks/i });
    expect(within(makePicksButton).getByTestId('SportsFootballIcon')).toBeInTheDocument();
    expect(within(makePicksButton).queryByTestId('SportsTennisIcon')).not.toBeInTheDocument();
  });
});

describe('HomePage — promo video', () => {
  // frizat-f29: the video had no native controls at all, so viewers couldn't resize or
  // fullscreen it — only a custom mute button was rendered on top.
  it('renders the promo video with native controls enabled', () => {
    renderPage();
    const video = document.querySelector('video');
    expect(video).toHaveAttribute('controls');
  });
});

describe('HomePage — desktop widget placement (frizat-7c4)', () => {
  // frizat-7c4: on desktop, the hero image column used to stack the hero image, PicksIsland,
  // DashboardStandings, AND OwnerCostSummary (4 items) while the text column only had the
  // greeting + buttons (1 "item") — the taller column dictated overall page height, adding
  // scroll that the shorter column had unused space for. Moving Picks/Standings under the
  // greeting balances both columns to ~2 items each. Mobile is unaffected either way, since
  // Grid already collapses to one column there regardless of which column an item is in.
  const league: LeagueUserMappingDto = {
    id: 1, leagueId: 1, userId: 'user-1', leagueName: 'Test League', leagueType: 0, dateCreated: '',
  };
  const ownedLeague: LeagueInfoDto = {
    id: 1, leagueName: 'Test League', leagueType: 'Nfl', ownerUserId: 'user-1', dateCreated: '2026-01-01T00:00:00Z',
  };
  // Matches the makeAdapter(...) factory convention already established in PicksIsland.test.tsx
  // and DashboardStandings.test.tsx, rather than a one-off differently-shaped inline const.
  function makeAdapter(): SportAdapter {
    return {
      sport: 'nfl',
      weekSelectorConfig: {},
      currentSeasonYear: async () => 2025,
      loadCurrentGames: vi.fn().mockResolvedValue(null),
    } as unknown as SportAdapter;
  }

  beforeEach(() => {
    authState.user = { userId: 'user-1', name: 'Alice', claims: [] };
    sessionState.leaguesLoaded = true;
    sessionState.availableLeagues = [league];
    sessionState.ownedLeagues = [ownedLeague];
    mockedGetLeagueJuice.mockResolvedValue([]);
    mockedGetLeagueCost.mockResolvedValue({ memberCount: 1, cost: 50 });
    mockedGetLeaderboard.mockResolvedValue([]);
  });

  it('renders Picks and Standings inside the greeting column, and Costs inside the hero image column', async () => {
    renderPage(makeAdapter());

    const picksIsland = await screen.findByTestId('picks-island');
    const dashboardStandings = await screen.findByTestId('dashboard-standings');
    const ownerCostSummary = await screen.findByTestId('owner-cost-summary');

    const textSection = document.querySelector('.hero-text-section');
    const imageSection = document.querySelector('.hero-image-section');

    expect(textSection).toContainElement(picksIsland);
    expect(textSection).toContainElement(dashboardStandings);
    expect(imageSection).toContainElement(ownerCostSummary);

    expect(imageSection).not.toContainElement(picksIsland);
    expect(imageSection).not.toContainElement(dashboardStandings);
    expect(textSection).not.toContainElement(ownerCostSummary);
  });
});
