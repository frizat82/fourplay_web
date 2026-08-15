import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { vi } from 'vitest';
import LeaguePortalPage from '../pages/LeaguePortalPage';
import type { LeagueInfoDto, LeagueJuiceMappingDto, LeagueCostDto, UserSummaryDto } from '../types/admin';
import type { LeagueUserMappingDto } from '../types/league';
import type { UserInfo } from '../types/auth';

// OwnerCostSummary (rendered inside LeaguePortalPage) uses react-query.
function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: Infinity } } });
  return render(
    <QueryClientProvider client={client}>
      <LeaguePortalPage />
    </QueryClientProvider>,
  );
}

const sessionState = {
  ownedLeagues: [] as LeagueInfoDto[],
  leaguesLoaded: true,
  reloadLeagues: vi.fn().mockResolvedValue(undefined),
  currentLeague: null as number | null,
};

const OWNER_USER: UserInfo = { userId: 'owner-1', name: 'frizat', claims: [] };
const ADMIN_USER: UserInfo = { userId: 'admin-1', name: 'Admin', claims: [{ type: 'role', value: 'Administrator' }] };

const authState = { user: OWNER_USER as UserInfo | null };
const sportContext = { sport: 'NFL' as 'NFL' | 'CFB', isCfb: false, isNfl: true };
const toastPush = vi.fn();

vi.mock('../services/session', () => ({ useSession: () => sessionState }));
vi.mock('../services/sport', () => ({ useSportContext: () => sportContext }));
vi.mock('../services/auth', () => ({ useAuth: () => authState }));
vi.mock('../services/toast', () => ({ useToast: () => ({ push: toastPush }) }));

vi.mock('../api/league', () => ({
  getLeagueUserMappings: vi.fn(),
  getLeagueJuice: vi.fn(),
  getLeagueCost: vi.fn(),
  updateLeagueJuice: vi.fn(),
  rollForwardJuice: vi.fn(),
  removeLeagueMember: vi.fn(),
  inviteToLeague: vi.fn(),
  getAllLeagues: vi.fn(),
  getUsers: vi.fn(),
  createLeague: vi.fn(),
  addLeagueUserMapping: vi.fn(),
  assignLeagueOwner: vi.fn(),
}));
import {
  getLeagueUserMappings,
  getLeagueJuice,
  getLeagueCost,
  getAllLeagues,
  getUsers,
  createLeague,
  addLeagueUserMapping,
} from '../api/league';

const mockedGetMappings = vi.mocked(getLeagueUserMappings);
const mockedGetJuice = vi.mocked(getLeagueJuice);
const mockedGetCost = vi.mocked(getLeagueCost);
const mockedGetAllLeagues = vi.mocked(getAllLeagues);
const mockedGetUsers = vi.mocked(getUsers);
const mockedCreateLeague = vi.mocked(createLeague);
const mockedAddLeagueUserMapping = vi.mocked(addLeagueUserMapping);

const CURRENT_SEASON = new Date().getFullYear();

function makeLeague(overrides: Partial<LeagueInfoDto> = {}): LeagueInfoDto {
  return {
    id: 1,
    leagueName: 'Demo League',
    leagueType: 'Nfl',
    ownerUserId: 'owner-1',
    dateCreated: '2026-06-29T00:00:00Z',
    ...overrides,
  };
}

function makeMember(): LeagueUserMappingDto {
  return {
    id: 1,
    leagueId: 1,
    userId: '562e8450-7f22-4ab2-9cfa-5ded8c1091af',
    userName: 'frizat',
    email: 'frizat@example.com',
    leagueType: 0,
    dateCreated: '2026-06-29T00:00:00Z',
  };
}

function makeJuice(season: number): LeagueJuiceMappingDto {
  return {
    id: 1,
    leagueId: 1,
    leagueName: 'Demo League',
    season,
    juice: 13,
    juiceDivisional: 10,
    juiceConference: 6,
    weeklyCost: 5,
    dateCreated: '2026-06-29T00:00:00Z',
  };
}

function makeUser(overrides: Partial<UserSummaryDto> = {}): UserSummaryDto {
  return { id: 'user-2', userName: 'bob', email: 'bob@example.com', emailConfirmed: true, isAdmin: false, ...overrides };
}

const cost: LeagueCostDto = { memberCount: 1, cost: 100 };

beforeEach(() => {
  authState.user = OWNER_USER;
  sportContext.sport = 'NFL';
  sportContext.isCfb = false;
  sportContext.isNfl = true;
  sessionState.ownedLeagues = [makeLeague()];
  mockedGetMappings.mockResolvedValue([makeMember()]);
  mockedGetCost.mockResolvedValue(cost);
  mockedGetJuice.mockResolvedValue([makeJuice(CURRENT_SEASON - 1)]);
  mockedGetAllLeagues.mockResolvedValue([]);
  mockedGetUsers.mockResolvedValue([makeUser()]);
  mockedCreateLeague.mockResolvedValue(makeLeague({ id: 99, leagueName: 'New League' }));
  mockedAddLeagueUserMapping.mockResolvedValue(undefined);
  sessionState.reloadLeagues.mockClear();
  toastPush.mockClear();
});

describe('LeaguePortalPage (owner, non-admin)', () => {
  it('shows the member email, not the raw user id', async () => {
    renderPage();
    await screen.findByText('frizat@example.com');
    expect(screen.queryByText('562e8450-7f22-4ab2-9cfa-5ded8c1091af')).not.toBeInTheDocument();
  });

  it('locks juice fields for a past season and keeps them editable for the current season', async () => {
    renderPage();
    await userEvent.click(await screen.findByRole('tab', { name: 'Juice Settings' }));

    const seasonSelect = screen.getAllByRole('combobox')[0];
    await userEvent.click(seasonSelect);
    await userEvent.click(await screen.findByRole('option', { name: String(CURRENT_SEASON - 1) }));

    await waitFor(() => expect(screen.getByLabelText(/Tease Pts \(Regular Season\)/i)).toHaveValue(13));
    expect(screen.getByLabelText(/Tease Pts \(Regular Season\)/i)).toBeDisabled();
    expect(screen.getByRole('button', { name: /save/i })).toBeDisabled();

    await userEvent.click(seasonSelect);
    await userEvent.click(await screen.findByRole('option', { name: String(CURRENT_SEASON) }));

    await waitFor(() => expect(screen.getByLabelText(/Tease Pts \(Regular Season\)/i)).not.toBeDisabled());
    expect(screen.getByRole('button', { name: /save/i })).not.toBeDisabled();
  });

  it('does not show a raw owner id on the Info tab', async () => {
    renderPage();
    await userEvent.click(await screen.findByRole('tab', { name: 'Info' }));
    expect(screen.queryByText(/owner id/i)).not.toBeInTheDocument();
  });

  // frizat-d6l: any authenticated user can self-serve create a league (and becomes its
  // owner) — but Add User / Change Owner stay admin-only (they need the platform-wide user
  // list, which is itself a privileged endpoint).
  it('offers self-serve Create League but not Add User or Change Owner', async () => {
    renderPage();
    await screen.findByText('frizat@example.com');
    expect(screen.getByRole('button', { name: /create league/i })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /add user/i })).not.toBeInTheDocument();
    await userEvent.click(await screen.findByRole('tab', { name: 'Info' }));
    expect(screen.queryByRole('button', { name: /change owner/i })).not.toBeInTheDocument();
  });

  it('does not fetch the admin-only all-leagues or all-users endpoints', async () => {
    renderPage();
    await screen.findByText('frizat@example.com');
    expect(mockedGetAllLeagues).not.toHaveBeenCalled();
    expect(mockedGetUsers).not.toHaveBeenCalled();
  });

  it('self-serve Create League has no Owner picker and makes the caller the owner', async () => {
    renderPage();
    await userEvent.click(await screen.findByRole('button', { name: /create league/i }));

    expect(await screen.findByRole('dialog')).toBeInTheDocument();
    expect(screen.queryByLabelText(/^owner$/i)).not.toBeInTheDocument();

    await userEvent.type(screen.getByLabelText(/league name/i), 'New League');
    await userEvent.click(screen.getByRole('button', { name: /^create league$/i }));

    await waitFor(() => expect(mockedCreateLeague).toHaveBeenCalledWith(
      expect.objectContaining({ leagueName: 'New League', ownerUserId: 'owner-1' })
    ));
    expect(sessionState.reloadLeagues).toHaveBeenCalled();
  });
});

describe('LeaguePortalPage (no leagues yet)', () => {
  it('shows an empty state with a Create League call to action instead of a dead end', async () => {
    sessionState.ownedLeagues = [];
    renderPage();

    expect(await screen.findByText(/you don.t have any leagues yet/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /create league/i })).toBeInTheDocument();
  });
});

describe('LeaguePortalPage (site admin)', () => {
  beforeEach(() => {
    authState.user = ADMIN_USER;
    sessionState.ownedLeagues = [];
    sessionState.currentLeague = null;
    // Two leagues per sport so the league-picker <Select> renders in both sport contexts below
    // (LeaguePortalPage hides it entirely when there's only one option to choose from).
    mockedGetAllLeagues.mockResolvedValue([
      makeLeague({ id: 1, leagueName: 'Demo League', leagueType: 'Nfl' }),
      makeLeague({ id: 3, leagueName: 'Second NFL League', leagueType: 'Nfl', ownerUserId: 'someone-else' }),
      makeLeague({ id: 2, leagueName: 'CFB Demo League', leagueType: 'Cfb', ownerUserId: 'someone-else' }),
      makeLeague({ id: 4, leagueName: 'Second CFB League', leagueType: 'Cfb', ownerUserId: 'someone-else' }),
    ]);
  });

  it('lists every league platform-wide for the current sport, not just owned ones', async () => {
    renderPage();
    await screen.findAllByRole('combobox');
    await userEvent.click(screen.getAllByRole('combobox')[0]);
    // "someone-else"-owned NFL league still shows — admin sees platform-wide, not just owned.
    expect(await screen.findByRole('option', { name: /Second NFL League/i })).toBeInTheDocument();
  });

  // frizat: opening My Leagues should default to whichever league is active in the top-right
  // league switcher (session.currentLeague), not always the first owned/available league —
  // otherwise it opens on an arbitrary league unrelated to what you were just looking at.
  it('defaults the selected league to the one active in the top-right switcher', async () => {
    sessionState.currentLeague = 3; // "Second NFL League", not the first option in the list
    renderPage();
    await screen.findByText('Second NFL League');
  });

  it('falls back to the first available league when the switcher is on one you don\'t administer', async () => {
    sessionState.currentLeague = 999; // not in leagueOptions at all
    renderPage();
    await screen.findByText('Demo League');
  });

  // frizat: the page title alone ("My Leagues") doesn't say which league you're looking at when
  // the picker is hidden (single-league case) — the subtitle should always name it.
  it('shows the selected league\'s name in the page subtitle', async () => {
    sportContext.sport = 'CFB';
    sportContext.isCfb = true;
    sportContext.isNfl = false;
    sessionState.currentLeague = 2; // "CFB Demo League"
    renderPage();
    await screen.findByText('CFB Demo League');
  });

  // frizat: My Leagues must stay scoped to the current sport even for admins — ownedLeagues
  // already applies this filter for non-admins (session.tsx), but admins use the separate
  // platform-wide allLeagues list, which has no sport filter applied at the API layer.
  it('does not show leagues from the other sport, even for admins', async () => {
    renderPage();
    await screen.findAllByRole('combobox');
    await userEvent.click(screen.getAllByRole('combobox')[0]);
    expect(screen.queryByRole('option', { name: /CFB Demo League/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('option', { name: /Second CFB League/i })).not.toBeInTheDocument();
  });

  it('shows only CFB leagues when on the CFB domain', async () => {
    sportContext.sport = 'CFB';
    sportContext.isCfb = true;
    sportContext.isNfl = false;

    renderPage();
    await screen.findAllByRole('combobox');
    await userEvent.click(screen.getAllByRole('combobox')[0]);
    expect(await screen.findByRole('option', { name: /Second CFB League/i })).toBeInTheDocument();
    expect(screen.queryByRole('option', { name: /^Demo League$/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('option', { name: /Second NFL League/i })).not.toBeInTheDocument();
  });

  // frizat-d6l follow-up: leagueOptions starts as [] for admins too (allLeagues loads async), so
  // the empty state must wait for that fetch — otherwise an admin with leagues platform-wide sees
  // a false "no leagues yet" flash before the real list renders.
  it('does not show the no-leagues empty state while the platform-wide league list is still loading', async () => {
    let resolveAllLeagues!: (leagues: LeagueInfoDto[]) => void;
    mockedGetAllLeagues.mockReturnValue(new Promise((resolve) => { resolveAllLeagues = resolve; }));

    renderPage();
    expect(screen.queryByText(/you don.t have any leagues yet/i)).not.toBeInTheDocument();

    resolveAllLeagues([makeLeague({ id: 1, leagueName: 'Demo League', leagueType: 'Nfl' })]);
    await screen.findByRole('tab', { name: 'Members' });
    expect(screen.queryByText(/you don.t have any leagues yet/i)).not.toBeInTheDocument();
  });

  it('offers Create League, Add User, and Change Owner', async () => {
    renderPage();
    expect(await screen.findByRole('button', { name: /create league/i })).toBeInTheDocument();
    await screen.findByText('frizat@example.com');
    expect(screen.getByRole('button', { name: /add user/i })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('tab', { name: 'Info' }));
    expect(screen.getByRole('button', { name: /change owner/i })).toBeInTheDocument();
  });

  // frizat: getUsers() backs Add User, Create League's owner picker, and Assign Owner —
  // a failed fetch previously left all three silently empty forever (no catch, no toast,
  // no retry). This proves the failure is now surfaced instead of invisible.
  it('shows an error toast when the platform user list fails to load', async () => {
    mockedGetUsers.mockRejectedValue(new Error('network down'));
    renderPage();
    await screen.findByText('frizat@example.com');
    await waitFor(() => expect(toastPush).toHaveBeenCalledWith(expect.stringMatching(/failed to load users/i), 'error'));
  });

  it('adds a selected user to the league via the Add User dialog', async () => {
    renderPage();
    await userEvent.click(await screen.findByRole('button', { name: /add user/i }));

    const dialog = await screen.findByRole('dialog');
    const userSelect = within(dialog).getByLabelText(/^user$/i);
    await userEvent.selectOptions(userSelect, 'bob@example.com');
    await userEvent.click(within(dialog).getByRole('button', { name: /^add user$/i }));

    await waitFor(() => expect(mockedAddLeagueUserMapping).toHaveBeenCalledWith(1, 'user-2'));
    expect(toastPush).toHaveBeenCalledWith('bob@example.com added to league', 'success');
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });
});
