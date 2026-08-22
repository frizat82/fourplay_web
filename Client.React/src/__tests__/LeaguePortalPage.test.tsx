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
  deleteLeague: vi.fn(),
  generateInviteLink: vi.fn(),
  getCurrentInviteLink: vi.fn().mockResolvedValue(null),
  getLeagueInvitations: vi.fn().mockResolvedValue([]),
  getLeagueMembershipInvites: vi.fn().mockResolvedValue([]),
  cancelMembershipInvite: vi.fn(),
}));
import {
  getLeagueUserMappings,
  getLeagueJuice,
  getLeagueCost,
  getAllLeagues,
  getUsers,
  createLeague,
  addLeagueUserMapping,
  deleteLeague,
  getCurrentInviteLink,
  getLeagueInvitations,
  getLeagueMembershipInvites,
  cancelMembershipInvite,
  generateInviteLink,
  inviteToLeague,
  type LeagueInviteLinkDto,
  type InvitationDto,
  type MembershipInviteStatusDto,
} from '../api/league';

const mockedGetMappings = vi.mocked(getLeagueUserMappings);
const mockedGetJuice = vi.mocked(getLeagueJuice);
const mockedGetCost = vi.mocked(getLeagueCost);
const mockedGetAllLeagues = vi.mocked(getAllLeagues);
const mockedGetCurrentInviteLink = vi.mocked(getCurrentInviteLink);
const mockedGetLeagueInvitations = vi.mocked(getLeagueInvitations);
const mockedGetLeagueMembershipInvites = vi.mocked(getLeagueMembershipInvites);
const mockedCancelMembershipInvite = vi.mocked(cancelMembershipInvite);
const mockedGenerateInviteLink = vi.mocked(generateInviteLink);
const mockedInviteToLeague = vi.mocked(inviteToLeague);
const mockedGetUsers = vi.mocked(getUsers);
const mockedCreateLeague = vi.mocked(createLeague);
const mockedAddLeagueUserMapping = vi.mocked(addLeagueUserMapping);
const mockedDeleteLeague = vi.mocked(deleteLeague);

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
  mockedDeleteLeague.mockResolvedValue(undefined);
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
    await userEvent.click(await screen.findByRole('tab', { name: 'Settings' }));

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

  // frizat: "Juice Settings"/"Weekly Cost" read as gambling jargon to a general audience —
  // renamed to plain language. Locking in the new copy so this doesn't silently regress.
  it('shows the Settings tab with plain-language labels (no gambling jargon)', async () => {
    renderPage();
    await userEvent.click(await screen.findByRole('tab', { name: 'Settings' }));
    expect(await screen.findByLabelText(/cost per week/i)).toBeInTheDocument();
    expect(screen.queryByRole('tab', { name: /juice settings/i })).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/weekly cost/i)).not.toBeInTheDocument();
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

  // frizat: Remove Member is now a soft-delete (kept for audit/history, can be re-added) — the
  // dialog copy must say so, not "cannot be undone", which stopped being true.
  it('tells the admin a removed member can be re-added, not that it cannot be undone', async () => {
    renderPage();
    await userEvent.click(await screen.findByRole('button', { name: /remove/i }));

    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText(/re-added/i)).toBeInTheDocument();
    expect(within(dialog).queryByText(/cannot be undone/i)).not.toBeInTheDocument();
  });

  // frizat: deleting an entire league (all picks, members, payout history) is a much bigger
  // blast radius than removing one member, so a plain Cancel/Confirm click is too easy to
  // mis-click — the confirm button stays disabled until the league name is typed exactly.
  it('gates Delete League behind typing the exact league name, then deletes it', async () => {
    renderPage();
    await userEvent.click(await screen.findByRole('tab', { name: 'Info' }));
    await userEvent.click(await screen.findByRole('button', { name: /delete league/i }));

    const dialog = await screen.findByRole('dialog');
    const confirmButton = within(dialog).getByRole('button', { name: /^delete league$/i });
    expect(confirmButton).toBeDisabled();

    const confirmInput = within(dialog).getByLabelText(/league name/i);
    await userEvent.type(confirmInput, 'Demo Leagu');
    expect(confirmButton).toBeDisabled();

    await userEvent.type(confirmInput, 'e');
    expect(confirmButton).not.toBeDisabled();

    await userEvent.click(confirmButton);

    await waitFor(() => expect(mockedDeleteLeague).toHaveBeenCalledWith(1));
    expect(toastPush).toHaveBeenCalledWith(expect.stringMatching(/deleted/i), 'success');
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

  // frizat: Create League's Sport field used to be a free-choice dropdown regardless of which
  // subdomain the admin was on — an admin browsing cfb.* could create an NFL league and vice
  // versa. The site you're on IS the sport you're creating for, so the field should show the
  // current sport and not offer the other one at all.
  it('locks the Create League sport field to the current subdomain, not an editable choice', async () => {
    sportContext.sport = 'CFB';
    sportContext.isCfb = true;
    sportContext.isNfl = false;
    renderPage();
    await userEvent.click(await screen.findByRole('button', { name: /create league/i }));

    const dialog = await screen.findByRole('dialog');
    const sportField = within(dialog).getByLabelText(/^sport$/i);
    expect(sportField).toHaveValue('CFB');
    expect(sportField).toBeDisabled();
    expect(within(dialog).queryByRole('option', { name: /^nfl$/i })).not.toBeInTheDocument();
  });

  it('locks the Create League sport field to NFL on the NFL subdomain', async () => {
    renderPage();
    await userEvent.click(await screen.findByRole('button', { name: /create league/i }));

    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByLabelText(/^sport$/i)).toHaveValue('NFL');
  });
});

describe('LeaguePortalPage — invite link and sent invitations', () => {
  function makeInviteLink(overrides: Partial<LeagueInviteLinkDto> = {}): LeagueInviteLinkDto {
    return {
      token: 'abc123',
      leagueId: 1,
      leagueName: 'Demo League',
      expiresAt: new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString(),
      ...overrides,
    };
  }

  function makeInvitation(overrides: Partial<InvitationDto> = {}): InvitationDto {
    return {
      id: 1,
      email: 'alice@example.com',
      createdAt: new Date().toISOString(),
      expiresAt: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString(),
      isUsed: false,
      isExpired: false,
      isValid: true,
      usedAt: null,
      ...overrides,
    };
  }

  function makeMembershipInvite(overrides: Partial<MembershipInviteStatusDto> = {}): MembershipInviteStatusDto {
    return {
      id: 1,
      leagueId: 1,
      invitedUserEmail: 'bob@example.com',
      invitedUserName: 'bob',
      status: 'Pending',
      createdAt: new Date().toISOString(),
      respondedAt: null,
      ...overrides,
    };
  }

  beforeEach(() => {
    authState.user = OWNER_USER;
    sessionState.ownedLeagues = [makeLeague()];
    mockedGetMappings.mockResolvedValue([makeMember()]);
    mockedGetCost.mockResolvedValue(cost);
    mockedGetJuice.mockResolvedValue([makeJuice(CURRENT_SEASON - 1)]);
    // Default: no existing invite link, no sent invitations
    mockedGetCurrentInviteLink.mockResolvedValue(null);
    mockedGetLeagueInvitations.mockResolvedValue([]);
    mockedGetLeagueMembershipInvites.mockResolvedValue([]);
    sessionState.reloadLeagues.mockClear();
    toastPush.mockClear();
  });

  it('fetches the current invite link and invitations when a league is selected', async () => {
    renderPage();
    await screen.findByText('frizat@example.com');

    expect(mockedGetCurrentInviteLink).toHaveBeenCalledWith(1);
    expect(mockedGetLeagueInvitations).toHaveBeenCalledWith(1);
  });

  it('shows the active invite link panel with copy and share buttons when a link exists', async () => {
    mockedGetCurrentInviteLink.mockResolvedValue(makeInviteLink());
    renderPage();
    await screen.findByText('frizat@example.com');

    expect(await screen.findByRole('button', { name: /^copy$/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /^share$/i })).toBeInTheDocument();
  });

  it('shows the expired-link warning and no copy/share buttons when the link is expired', async () => {
    mockedGetCurrentInviteLink.mockResolvedValue(makeInviteLink({
      expiresAt: new Date(Date.now() - 1000).toISOString(),
    }));
    renderPage();
    await screen.findByText('frizat@example.com');

    expect(await screen.findByText(/link expired/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /copy link/i })).not.toBeInTheDocument();
  });

  it('shows the Sent Invitations table when there are pending invitations', async () => {
    mockedGetLeagueInvitations.mockResolvedValue([makeInvitation()]);
    renderPage();
    await screen.findByText('frizat@example.com');

    expect(await screen.findByText('Sent Invitations')).toBeInTheDocument();
    expect(await screen.findByText('alice@example.com')).toBeInTheDocument();
    expect(await screen.findByText(/pending/i)).toBeInTheDocument();
  });

  it('shows Confirmed chip for a used invitation whose registered user has confirmed their email', async () => {
    mockedGetLeagueInvitations.mockResolvedValue([
      makeInvitation({ isUsed: true, isValid: false, registeredUserEmailConfirmed: true }),
    ]);
    renderPage();
    await screen.findByText('frizat@example.com');

    expect(await screen.findByText(/^confirmed$/i)).toBeInTheDocument();
  });

  it('shows Pending Confirmation chip for a used invitation whose registered user has not confirmed yet', async () => {
    // Same confusion the admin Invitations page fix resolves — a plain "Accepted" here would
    // hide that the registered user is still stuck unable to log in.
    mockedGetLeagueInvitations.mockResolvedValue([
      makeInvitation({ isUsed: true, isValid: false, registeredUserEmailConfirmed: false }),
    ]);
    renderPage();
    await screen.findByText('frizat@example.com');

    expect(await screen.findByText(/pending confirmation/i)).toBeInTheDocument();
  });

  it('shows Expired chip for an expired, unused invitation', async () => {
    mockedGetLeagueInvitations.mockResolvedValue([makeInvitation({ isExpired: true, isValid: false })]);
    renderPage();
    await screen.findByText('frizat@example.com');

    expect(await screen.findByText(/expired/i)).toBeInTheDocument();
  });

  it('refreshes the invitations list after sending an email invite', async () => {
    const refreshedInvitation = makeInvitation({ email: 'bob@example.com' });
    mockedInviteToLeague.mockResolvedValue({ email: 'bob@example.com', outcome: 'NewUserInvitationSent' });
    mockedGetLeagueInvitations
      .mockResolvedValueOnce([])
      .mockResolvedValueOnce([refreshedInvitation]);

    renderPage();
    await screen.findByText('frizat@example.com');
    await userEvent.click(screen.getByRole('button', { name: /invite player/i }));
    const dialog = await screen.findByRole('dialog');
    await userEvent.type(within(dialog).getByLabelText(/email/i), 'bob@example.com');
    await userEvent.click(within(dialog).getByRole('button', { name: /^send invite$/i }));

    await waitFor(() => expect(mockedGetLeagueInvitations).toHaveBeenCalledTimes(2));
    expect(await screen.findByText('bob@example.com')).toBeInTheDocument();
  });

  it('inviting an already-registered email shows a pending-acceptance message, not an instant add', async () => {
    // Someone who already has an account gets a pending invite they must explicitly accept —
    // not added instantly with no consent. No members-list refresh: nobody was added yet.
    mockedInviteToLeague.mockResolvedValue({ email: 'bob@example.com', outcome: 'ExistingUserInvitePending' });

    renderPage();
    await screen.findByText('frizat@example.com');
    await userEvent.click(screen.getByRole('button', { name: /invite player/i }));
    const dialog = await screen.findByRole('dialog');
    await userEvent.type(within(dialog).getByLabelText(/email/i), 'bob@example.com');
    await userEvent.click(within(dialog).getByRole('button', { name: /^send invite$/i }));

    await waitFor(() => expect(toastPush).toHaveBeenCalledWith(expect.stringMatching(/pending their acceptance/i), 'success'));
    expect(mockedGetMappings).toHaveBeenCalledTimes(1); // only the initial load, no refresh
  });

  it('shows the server\'s specific conflict message when inviting someone already on the league', async () => {
    mockedInviteToLeague.mockRejectedValue(
      Object.assign(new Error('Conflict'), {
        isAxiosError: true,
        response: { status: 409, data: 'bob@example.com is already a member of this league.' },
      }),
    );

    renderPage();
    await screen.findByText('frizat@example.com');
    await userEvent.click(screen.getByRole('button', { name: /invite player/i }));
    const dialog = await screen.findByRole('dialog');
    await userEvent.type(within(dialog).getByLabelText(/email/i), 'bob@example.com');
    await userEvent.click(within(dialog).getByRole('button', { name: /^send invite$/i }));

    await waitFor(() =>
      expect(toastPush).toHaveBeenCalledWith('bob@example.com is already a member of this league.', 'error'),
    );
  });

  it('updates the invite link state after generating a new link', async () => {
    const newLink = makeInviteLink({ token: 'newtoken' });
    mockedGenerateInviteLink.mockResolvedValue(newLink);
    renderPage();
    await screen.findByText('frizat@example.com');

    await userEvent.click(screen.getByRole('button', { name: /generate invite link/i }));

    await waitFor(() => expect(mockedGenerateInviteLink).toHaveBeenCalledWith(1));
    expect(await screen.findByRole('button', { name: /^copy$/i })).toBeInTheDocument();
  });

  it('shows a pending membership invite with a Cancel button, and cancels it', async () => {
    mockedGetLeagueMembershipInvites
      .mockResolvedValueOnce([makeMembershipInvite()])
      .mockResolvedValueOnce([]);
    mockedCancelMembershipInvite.mockResolvedValue(undefined);

    renderPage();
    await screen.findByText('frizat@example.com');

    expect(await screen.findByText('bob@example.com')).toBeInTheDocument();
    expect(screen.getByText('Pending')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: /^cancel$/i }));

    await waitFor(() => expect(mockedCancelMembershipInvite).toHaveBeenCalledWith(1));
    await waitFor(() => expect(mockedGetLeagueMembershipInvites).toHaveBeenCalledTimes(2));
  });

  it('does not show a Cancel button for an already-accepted or declined membership invite', async () => {
    mockedGetLeagueMembershipInvites.mockResolvedValue([
      makeMembershipInvite({ id: 2, status: 'Accepted' }),
    ]);

    renderPage();
    await screen.findByText('frizat@example.com');

    expect(await screen.findByText('Accepted')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^cancel$/i })).not.toBeInTheDocument();
  });
});
