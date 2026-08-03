import { render, screen, waitFor } from '@testing-library/react';
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
  isLeagueOwner: true,
  reloadLeagues: vi.fn().mockResolvedValue(undefined),
};

const OWNER_USER: UserInfo = { userId: 'owner-1', name: 'frizat', claims: [] };
const ADMIN_USER: UserInfo = { userId: 'admin-1', name: 'Admin', claims: [{ type: 'role', value: 'Administrator' }] };

const authState = { user: OWNER_USER as UserInfo | null };

vi.mock('../services/session', () => ({ useSession: () => sessionState }));
vi.mock('../services/auth', () => ({ useAuth: () => authState }));
vi.mock('../services/toast', () => ({ useToast: () => ({ push: vi.fn() }) }));

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
} from '../api/league';

const mockedGetMappings = vi.mocked(getLeagueUserMappings);
const mockedGetJuice = vi.mocked(getLeagueJuice);
const mockedGetCost = vi.mocked(getLeagueCost);
const mockedGetAllLeagues = vi.mocked(getAllLeagues);
const mockedGetUsers = vi.mocked(getUsers);
const mockedCreateLeague = vi.mocked(createLeague);

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
  sessionState.ownedLeagues = [makeLeague()];
  sessionState.isLeagueOwner = true;
  mockedGetMappings.mockResolvedValue([makeMember()]);
  mockedGetCost.mockResolvedValue(cost);
  mockedGetJuice.mockResolvedValue([makeJuice(CURRENT_SEASON - 1)]);
  mockedGetAllLeagues.mockResolvedValue([]);
  mockedGetUsers.mockResolvedValue([makeUser()]);
  mockedCreateLeague.mockResolvedValue(makeLeague({ id: 99, leagueName: 'New League' }));
  sessionState.reloadLeagues.mockClear();
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
    sessionState.isLeagueOwner = false;
    sessionState.ownedLeagues = [];
    mockedGetAllLeagues.mockResolvedValue([
      makeLeague({ id: 1, leagueName: 'Demo League', leagueType: 'Nfl' }),
      makeLeague({ id: 2, leagueName: 'CFB Demo League', leagueType: 'Cfb', ownerUserId: 'someone-else' }),
    ]);
  });

  it('lists all leagues across sports, not just owned ones', async () => {
    renderPage();
    await screen.findByText('Demo League', { exact: false });
    await userEvent.click(screen.getAllByRole('combobox')[0]);
    expect(await screen.findByRole('option', { name: /CFB Demo League/i })).toBeInTheDocument();
  });

  it('offers Create League, Add User, and Change Owner', async () => {
    renderPage();
    expect(await screen.findByRole('button', { name: /create league/i })).toBeInTheDocument();
    await screen.findByText('frizat@example.com');
    expect(screen.getByRole('button', { name: /add user/i })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('tab', { name: 'Info' }));
    expect(screen.getByRole('button', { name: /change owner/i })).toBeInTheDocument();
  });
});
