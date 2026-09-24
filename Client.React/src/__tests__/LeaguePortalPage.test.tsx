import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { vi } from 'vitest';
import LeaguePortalPage from '../pages/LeaguePortalPage';
import { createNflAdapter } from '../services/nflAdapter';
import { createCfbAdapter } from '../services/cfbAdapter';
import type { LeagueInfoDto, LeagueJuiceMappingDto, LeagueCostDto, UserSummaryDto } from '../types/admin';
import type { LeagueUserMappingDto } from '../types/league';
import type { UserInfo } from '../types/auth';

// OwnerCostSummary (rendered inside LeaguePortalPage) uses react-query.
// frizat-8ni: a real adapter, not a standalone useMissingPicks(isCfb) branch — mirrors App.tsx's
// own LeaguePortalRoute wiring (isCfb ? cfbAdapter : nflAdapter). A fresh instance per render call
// so the adapter's own memoized current-week/slate resolution never leaks between tests.
function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: Infinity } } });
  const adapter = sportContext.isCfb ? createCfbAdapter() : createNflAdapter();
  return {
    client,
    ...render(
      <QueryClientProvider client={client}>
        <LeaguePortalPage adapter={adapter} />
      </QueryClientProvider>,
    ),
  };
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
  getNflCurrentWeek: vi.fn(),
  getLeaguePicks: vi.fn(),
  getLeaguePickCounts: vi.fn(),
}));
vi.mock('../api/cfb', () => ({
  getCfbCurrentSlate: vi.fn(),
  getCfbAllPicks: vi.fn(),
  getCfbPickCounts: vi.fn(),
}));
import { getCfbCurrentSlate, getCfbAllPicks, getCfbPickCounts } from '../api/cfb';
import {
  getLeagueUserMappings,
  getLeagueJuice,
  getLeagueCost,
  updateLeagueJuice,
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
  getNflCurrentWeek,
  getLeaguePicks,
  getLeaguePickCounts,
  type LeagueInviteLinkDto,
  type InvitationDto,
  type MembershipInviteStatusDto,
} from '../api/league';

const mockedGetMappings = vi.mocked(getLeagueUserMappings);
const mockedGetJuice = vi.mocked(getLeagueJuice);
const mockedGetCost = vi.mocked(getLeagueCost);
const mockedUpdateJuice = vi.mocked(updateLeagueJuice);
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
const mockedGetNflCurrentWeek = vi.mocked(getNflCurrentWeek);
const mockedGetLeaguePicks = vi.mocked(getLeaguePicks);
const mockedGetLeaguePickCounts = vi.mocked(getLeaguePickCounts);
const mockedGetCfbCurrentSlate = vi.mocked(getCfbCurrentSlate);
const mockedGetCfbAllPicks = vi.mocked(getCfbAllPicks);
const mockedGetCfbPickCounts = vi.mocked(getCfbPickCounts);

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

function makeJuice(season: number, overrides?: Partial<Pick<LeagueJuiceMappingDto, 'teaseLocked' | 'weeklyCostLocked'>>): LeagueJuiceMappingDto {
  return {
    id: 1,
    leagueId: 1,
    leagueName: 'Demo League',
    season,
    juice: 13,
    juiceDivisional: 10,
    juiceConference: 6,
    weeklyCost: 5,
    startWeek: 1,
    dateCreated: '2026-06-29T00:00:00Z',
    teaseLocked: false,
    weeklyCostLocked: false,
    ...overrides,
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
  // Default member fully picked, so pre-existing tests that don't care about frizat-6sc's
  // missing-picks column never see it show up as an unexpected "Missing" state.
  mockedGetNflCurrentWeek.mockResolvedValue({
    weekId: 2, season: CURRENT_SEASON, isPostSeason: false, weekLabel: 'Week 2', scoringFormat: 'Standard', spreadLockDatetime: '',
  });
  // frizat-xbq: counts come from the submitted-count endpoint. The visible-picks endpoints are
  // seeded EMPTY on purpose (that's what hides a member's pick on a game that hasn't kicked off) —
  // any test still passing proves the chip never depends on them.
  mockedGetLeaguePickCounts.mockResolvedValue([{ userId: makeMember().userId, pickCount: 4 }]);
  mockedGetLeaguePicks.mockResolvedValue([]);
  mockedGetCfbCurrentSlate.mockResolvedValue(null);
  mockedGetCfbPickCounts.mockResolvedValue([]);
  mockedGetJuice.mockResolvedValue([makeJuice(CURRENT_SEASON - 1)]);
  mockedUpdateJuice.mockResolvedValue(undefined);
  mockedGetAllLeagues.mockResolvedValue([]);
  mockedGetUsers.mockResolvedValue([makeUser()]);
  mockedCreateLeague.mockResolvedValue(makeLeague({ id: 99, leagueName: 'New League' }));
  mockedAddLeagueUserMapping.mockResolvedValue(undefined);
  mockedDeleteLeague.mockResolvedValue(undefined);
  sessionState.reloadLeagues.mockClear();
  toastPush.mockClear();
});

describe('LeaguePortalPage (owner, non-admin)', () => {
  // useLeagueJuice caches ['leagueJuice', leagueId] with a long staleTime so Picks/Scores don't
  // refetch it on every mount — which is only safe if the one page that edits juice keeps that
  // cache entry current. Otherwise an edited StartWeek would show stale on Picks for minutes.
  it('writes every juice load into the shared leagueJuice query cache', async () => {
    const initial = [makeJuice(CURRENT_SEASON)];
    mockedGetJuice.mockResolvedValue(initial);
    const { client } = renderPage();
    await userEvent.click(await screen.findByRole('tab', { name: 'Settings' }));
    await waitFor(() => expect(client.getQueryData(['leagueJuice', 1])).toEqual(initial));

    const updated = [{ ...makeJuice(CURRENT_SEASON), startWeek: 3 }];
    mockedGetJuice.mockResolvedValue(updated);
    await userEvent.click(screen.getByRole('button', { name: /save/i }));
    await waitFor(() => expect(client.getQueryData(['leagueJuice', 1])).toEqual(updated));
  });

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

  it('locks only the tease fields, not Weekly Cost, once the season has started', async () => {
    mockedGetJuice.mockResolvedValue([makeJuice(CURRENT_SEASON, { teaseLocked: true, weeklyCostLocked: false })]);
    renderPage();
    await userEvent.click(await screen.findByRole('tab', { name: 'Settings' }));

    await waitFor(() => expect(screen.getByLabelText(/Tease Pts \(Regular Season\)/i)).toHaveValue(13));
    expect(screen.getByLabelText(/Tease Pts \(Regular Season\)/i)).toBeDisabled();
    expect(screen.getByLabelText(/Tease Pts \(Divisional\)/i)).toBeDisabled();
    expect(screen.getByLabelText(/Tease Pts \(Conference\)/i)).toBeDisabled();
    expect(screen.getByLabelText(/Cost Per Week/i)).not.toBeDisabled();
    expect(screen.getByRole('button', { name: /save/i })).not.toBeDisabled();
    expect(screen.getByText(/Tease points are locked once the season has started/i)).toBeInTheDocument();
  });

  it('locks only Weekly Cost, not the tease fields, once the season\'s final week has started', async () => {
    mockedGetJuice.mockResolvedValue([makeJuice(CURRENT_SEASON, { teaseLocked: false, weeklyCostLocked: true })]);
    renderPage();
    await userEvent.click(await screen.findByRole('tab', { name: 'Settings' }));

    await waitFor(() => expect(screen.getByLabelText(/Cost Per Week/i)).toHaveValue(5));
    expect(screen.getByLabelText(/Cost Per Week/i)).toBeDisabled();
    expect(screen.getByLabelText(/Tease Pts \(Regular Season\)/i)).not.toBeDisabled();
    expect(screen.getByLabelText(/Tease Pts \(Divisional\)/i)).not.toBeDisabled();
    expect(screen.getByLabelText(/Tease Pts \(Conference\)/i)).not.toBeDisabled();
    expect(screen.getByRole('button', { name: /save/i })).not.toBeDisabled();
    expect(screen.getByText(/Weekly Cost is locked once the season's final week has started/i)).toBeInTheDocument();
  });

  // frizat-o3x: Start Week shares tease points' lock boundary — same rationale as Tease Pts.
  it('locks Start Week when tease is locked, not when only Weekly Cost is locked', async () => {
    mockedGetJuice.mockResolvedValue([makeJuice(CURRENT_SEASON, { teaseLocked: true, weeklyCostLocked: false })]);
    renderPage();
    await userEvent.click(await screen.findByRole('tab', { name: 'Settings' }));

    await waitFor(() => expect(screen.getByLabelText(/Tease Pts \(Regular Season\)/i)).toHaveValue(13));
    // MUI Select's displayed value/accessible name isn't reliably queryable in JSDOM (same gotcha
    // as the Season selector elsewhere in this file) — index into the comboboxes by DOM order
    // instead: [0] Season, [1] Start Week. aria-disabled reflects the FormControl's disabled state.
    expect(screen.getAllByRole('combobox')[1]).toHaveAttribute('aria-disabled', 'true');
  });

  it('leaves Start Week editable when only Weekly Cost is locked', async () => {
    mockedGetJuice.mockResolvedValue([makeJuice(CURRENT_SEASON, { teaseLocked: false, weeklyCostLocked: true })]);
    renderPage();
    await userEvent.click(await screen.findByRole('tab', { name: 'Settings' }));

    await waitFor(() => expect(screen.getByLabelText(/Cost Per Week/i)).toHaveValue(5));
    expect(screen.getAllByRole('combobox')[1]).not.toHaveAttribute('aria-disabled', 'true');
  });

  it('shows the season-starts-late explanation once Start Week is set above 1', async () => {
    mockedGetJuice.mockResolvedValue([{ ...makeJuice(CURRENT_SEASON), startWeek: 3 }]);
    renderPage();
    await userEvent.click(await screen.findByRole('tab', { name: 'Settings' }));

    await waitFor(() => expect(screen.getByText(/Weeks 1–2 won't require picks/i)).toBeInTheDocument());
  });

  it('shows the server\'s specific rejection message when a save is rejected by a lock, instead of a generic failure toast', async () => {
    mockedGetJuice.mockResolvedValue([makeJuice(CURRENT_SEASON)]);
    mockedUpdateJuice.mockRejectedValue(
      Object.assign(new Error('Bad Request'), {
        isAxiosError: true,
        response: { status: 400, data: "Tease points can't be changed once the season has started." },
      }),
    );

    renderPage();
    await userEvent.click(await screen.findByRole('tab', { name: 'Settings' }));
    await waitFor(() => expect(screen.getByLabelText(/Tease Pts \(Regular Season\)/i)).not.toBeDisabled());
    await userEvent.click(screen.getByRole('button', { name: /save/i }));

    await waitFor(() =>
      expect(toastPush).toHaveBeenCalledWith("Tease points can't be changed once the season has started.", 'error'));
  });

  it('lets the owner clear and retype a Tease Pts value without it snapping back mid-edit', async () => {
    // frizat: real user report — "can't type in #'s here" on desktop, "can't click up or down"
    // on mobile Chrome. Root cause: value={juiceForm.juice} (a NUMBER) fed straight from
    // onChange={(e) => onJuiceFormChange('juice', Number(e.target.value))} — every keystroke
    // immediately re-coerces through Number() and feeds the result back as the controlled
    // value, so clearing the field to retype gets silently overwritten back to a fully-parsed
    // number (e.g. "0") before the next character lands. Live-verified against the local demo
    // stack before writing this test.
    renderPage();
    await userEvent.click(await screen.findByRole('tab', { name: 'Settings' }));
    // The default beforeEach only seeds juice data for CURRENT_SEASON - 1, so the current
    // season's fields start at the form's zero-value default — irrelevant to this test, which
    // only cares about the clear/retype behavior, not the starting number.
    const field = await screen.findByLabelText(/Tease Pts \(Regular Season\)/i);
    await waitFor(() => expect(field).not.toBeDisabled());

    await userEvent.clear(field);
    expect(field).toHaveValue(null); // truly empty — not silently reset to 0

    await userEvent.type(field, '175');
    expect(field).toHaveValue(175);
  });

  // frizat: the backend's Juice DTO is `int` (Shared/Models/Data/Dtos/LeagueCreateDto.cs) —
  // a decimal typed here used to display fine but 400 silently on save. Strip the decimal
  // point as it's typed instead of catching the mismatch only after a failed save.
  it('strips a typed decimal point in Tease Pts instead of accepting it, since the field is whole-number only', async () => {
    renderPage();
    await userEvent.click(await screen.findByRole('tab', { name: 'Settings' }));
    const field = await screen.findByLabelText(/Tease Pts \(Regular Season\)/i);
    await waitFor(() => expect(field).not.toBeDisabled());

    await userEvent.clear(field);
    await userEvent.type(field, '17.5');
    expect(field).toHaveValue(175);
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

describe('LeaguePortalPage (frizat-6sc: commissioner missing-picks view)', () => {
  it('shows "Missing" for a member short on picks and "All Picked" for one who is done', async () => {
    mockedGetMappings.mockResolvedValue([
      makeMember(),
      { ...makeMember(), id: 2, userId: 'user-two', userName: 'bob', email: 'bob@example.com' },
    ]);
    // frizat's default beforeEach picks (4 of 4) stay untouched; bob has only submitted 2.
    mockedGetLeaguePickCounts.mockResolvedValue([
      { userId: '562e8450-7f22-4ab2-9cfa-5ded8c1091af', pickCount: 4 },
      { userId: 'user-two', pickCount: 2 },
    ]);

    renderPage();
    await screen.findByText('bob@example.com');

    await waitFor(() => expect(screen.getByTestId('missing-picks-1')).toHaveTextContent('All Picked'));
    expect(screen.getByTestId('missing-picks-2')).toHaveTextContent('Missing (2/4)');
  });

  // frizat-4bv: the column header shows the actual resolved week/slate instead of a generic
  // static label — reuses getCurrentWeek()'s own weekLabel, already fetched for getMissingPicks.
  it('shows the real current week label in the column header instead of the generic "This Week"', async () => {
    renderPage();
    await screen.findByText('frizat@example.com');

    expect(screen.getByText('Week 2')).toBeInTheDocument(); // default beforeEach's weekLabel
    expect(screen.queryByText('This Week')).not.toBeInTheDocument();
  });

  it('shows the real current slate label for CFB too', async () => {
    sportContext.sport = 'CFB';
    sportContext.isCfb = true;
    sportContext.isNfl = false;
    mockedGetCfbCurrentSlate.mockResolvedValue({
      id: 1, season: CURRENT_SEASON, slateNumber: 3, label: 'Week 3', slateType: 'RegularSeason', startDate: '', endDate: '',
    });
    mockedGetCfbPickCounts.mockResolvedValue([]);

    renderPage();
    await screen.findByText('frizat@example.com');

    expect(screen.getByText('Week 3')).toBeInTheDocument();
    expect(screen.queryByText('This Week')).not.toBeInTheDocument();
  });

  // /code-review: postseason labels ("Conference Championships", "CFP Quarterfinals", etc.) are
  // far longer than "This Week"/"Week 12" — the width this column was tuned around on a 390px
  // viewport (frizat-e3l dropped the "Joined" column specifically to fit this one without
  // horizontal scroll). Rather than guess a safe character budget, the header caps and ellipsizes
  // any label via CSS (with the full text always available via a native title tooltip) so it can
  // never force scroll back, regardless of how long a future week/slate label turns out to be.
  it('caps and ellipsizes a long postseason week label in the header instead of letting it force horizontal scroll', async () => {
    mockedGetNflCurrentWeek.mockResolvedValue({
      weekId: 21, season: CURRENT_SEASON, isPostSeason: true, weekLabel: 'Conference Championships', scoringFormat: 'Standard', spreadLockDatetime: '',
    });
    renderPage();
    await screen.findByText('frizat@example.com');

    const header = screen.getByText('Conference Championships');
    expect(header).toHaveStyle({ textOverflow: 'ellipsis', whiteSpace: 'nowrap' });
    expect(header).toHaveAttribute('title', 'Conference Championships');
  });

  // frizat-05h: getNflCurrentWeek() never legitimately resolves to "nothing" for NFL — it either
  // returns a real week or throws (nflAdapter.ts's own standing comment). A throw here is a real
  // control-table/network failure, not off-season — this test previously (wrongly) asserted the
  // same neutral "No Active Week" chip CFB's legitimate null-slate case gets, which meant a real
  // outage was indistinguishable from "nothing to pick right now." Split from the CFB off-season
  // test below, which is the actually-correct neutral-state case.
  it('surfaces a visible error state (not the neutral "No Active Week" chip) when the NFL current-week fetch genuinely fails', async () => {
    mockedGetNflCurrentWeek.mockRejectedValue(new Error('no current week configured'));

    renderPage();
    await screen.findByText('frizat@example.com');

    expect(screen.getByText('This Week')).toBeInTheDocument();
    await waitFor(() => expect(screen.getByTestId('missing-picks-1')).toHaveTextContent('Unavailable'));
    expect(screen.getByRole('alert')).toHaveTextContent(/couldn.t load this week.s picks/i);
  });

  it('retrying after a failed missing-picks fetch re-fetches and clears the error state', async () => {
    mockedGetNflCurrentWeek.mockRejectedValueOnce(new Error('no current week configured'));
    renderPage();
    await screen.findByText('frizat@example.com');
    await waitFor(() => expect(screen.getByTestId('missing-picks-1')).toHaveTextContent('Unavailable'));

    mockedGetNflCurrentWeek.mockResolvedValue({
      weekId: 2, season: CURRENT_SEASON, isPostSeason: false, weekLabel: 'Week 2', scoringFormat: 'Standard', spreadLockDatetime: '',
    });
    await userEvent.click(screen.getByRole('button', { name: /retry/i }));

    await waitFor(() => expect(screen.getByTestId('missing-picks-1')).toHaveTextContent('All Picked'));
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('keeps the "This Week" column visible with a neutral state when there is no current CFB slate (off-season)', async () => {
    sportContext.sport = 'CFB';
    sportContext.isCfb = true;
    sportContext.isNfl = false;
    mockedGetCfbCurrentSlate.mockResolvedValue(null);

    renderPage();
    await screen.findByText('frizat@example.com');

    expect(screen.getByText('This Week')).toBeInTheDocument();
    await waitFor(() => expect(screen.getByTestId('missing-picks-1')).toHaveTextContent('No Active Week'));
  });

  it('uses the CFB current-slate/all-picks path, not the NFL one, on the CFB site', async () => {
    sportContext.sport = 'CFB';
    sportContext.isCfb = true;
    sportContext.isNfl = false;
    mockedGetCfbCurrentSlate.mockResolvedValue({
      id: 1, season: CURRENT_SEASON, slateNumber: 3, label: 'Week 3', slateType: 'RegularSeason', startDate: '', endDate: '',
    });
    mockedGetCfbPickCounts.mockResolvedValue([]);

    renderPage();
    await screen.findByText('frizat@example.com');

    await waitFor(() => expect(screen.getByTestId('missing-picks-1')).toHaveTextContent('Missing (0/4)'));
    expect(mockedGetNflCurrentWeek).not.toHaveBeenCalled();
    expect(mockedGetCfbAllPicks).not.toHaveBeenCalled(); // counts, not the visible-picks endpoint
  });

  // frizat-xbq (Dhoward's real case): a member's 4th pick was on tonight's game — submitted, but the
  // visible-picks endpoint hides other users' picks until kickoff, so a non-admin commissioner saw
  // 3/4. The chip must count SUBMITTED picks (count endpoint), never the visible ones.
  it('counts a member\'s submitted picks even when the visible-picks endpoint would hide them', async () => {
    mockedGetMappings.mockResolvedValue([
      makeMember(),
      { ...makeMember(), id: 2, userId: 'user-two', userName: 'dhoward', email: 'dhoward@example.com' },
    ]);
    mockedGetLeaguePickCounts.mockResolvedValue([{ userId: 'user-two', pickCount: 4 }]);
    mockedGetLeaguePicks.mockResolvedValue([]); // what a non-admin owner is actually allowed to see

    renderPage();
    await screen.findByText('dhoward@example.com');

    await waitFor(() => expect(screen.getByTestId('missing-picks-2')).toHaveTextContent('All Picked'));
    expect(mockedGetLeaguePicks).not.toHaveBeenCalled();
  });

  // /simplify efficiency finding: the missing-picks fetch only matters while the Members tab
  // (where it renders) is actually showing — no point resolving current-week + all-picks data
  // for a commissioner sitting on Settings or Info.
  it('does not fetch missing-picks data while the Settings tab is active, only once back on Members', async () => {
    renderPage();
    await screen.findByText('frizat@example.com');
    mockedGetLeaguePickCounts.mockClear();

    await userEvent.click(screen.getByRole('tab', { name: 'Settings' }));
    await waitFor(() => expect(screen.getByLabelText(/Tease Pts \(Regular Season\)/i)).toBeInTheDocument());
    expect(mockedGetLeaguePickCounts).not.toHaveBeenCalled();

    await userEvent.click(screen.getByRole('tab', { name: 'Members' }));
    await waitFor(() => expect(mockedGetLeaguePickCounts).toHaveBeenCalled());
  });
});

describe('LeaguePortalPage (frizat-e3l: Missing Only filter)', () => {
  function seedTwoMembersOneMissing() {
    mockedGetMappings.mockResolvedValue([
      makeMember(),
      { ...makeMember(), id: 2, userId: 'user-two', userName: 'bob', email: 'bob@example.com' },
    ]);
    // frizat's default beforeEach picks (4 of 4) stay untouched; bob has only submitted 2.
    mockedGetLeaguePickCounts.mockResolvedValue([
      { userId: '562e8450-7f22-4ab2-9cfa-5ded8c1091af', pickCount: 4 },
      { userId: 'user-two', pickCount: 2 },
    ]);
  }

  it('shows a "Missing Only" toggle that hides fully-picked members and reveals only members still missing picks', async () => {
    seedTwoMembersOneMissing();
    renderPage();
    await screen.findByText('bob@example.com');
    expect(screen.getByText('frizat@example.com')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: /missing only/i }));

    expect(screen.getByText('bob@example.com')).toBeInTheDocument();
    expect(screen.queryByText('frizat@example.com')).not.toBeInTheDocument();
  });

  it('toggling back to "Show All Members" restores the full member list', async () => {
    seedTwoMembersOneMissing();
    renderPage();
    await screen.findByText('bob@example.com');

    await userEvent.click(screen.getByRole('button', { name: /missing only/i }));
    expect(screen.queryByText('frizat@example.com')).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: /show all members/i }));
    expect(await screen.findByText('frizat@example.com')).toBeInTheDocument();
  });

  // A member with no current week/slate to resolve ("No Active Week") isn't missing anything —
  // there's nothing to have picked yet — so the filter must exclude them, not include them as if
  // every off-season member were delinquent. CFB's legitimate null-current-slate resolution is
  // the only genuine "no active week" case — NFL's getNflCurrentWeek() never resolves to nothing,
  // it either succeeds or throws (frizat-05h), so this must not be simulated via a rejected NFL
  // mock (that's the 'error' case, covered separately below).
  it('excludes "No Active Week" members from the Missing Only filter, with a clear empty state', async () => {
    sportContext.sport = 'CFB';
    sportContext.isCfb = true;
    sportContext.isNfl = false;
    mockedGetCfbCurrentSlate.mockResolvedValue(null);
    renderPage();
    await screen.findByText('frizat@example.com');

    await userEvent.click(screen.getByRole('button', { name: /missing only/i }));

    expect(screen.queryByText('frizat@example.com')).not.toBeInTheDocument();
    expect(screen.getByText(/no members.*missing picks/i)).toBeInTheDocument();
  });

  // frizat-05h /code-review finding: a genuine fetch error must never produce the same confident-
  // looking empty state as "confirmed nobody's missing" — that's exactly the false-confidence
  // failure mode this bead exists to eliminate, just reachable through the filter instead of the
  // unfiltered view. The filter must not apply at all while the fetch is known to be broken.
  it('does not let the Missing Only filter claim "no members are missing" during a genuine fetch error', async () => {
    seedTwoMembersOneMissing();
    mockedGetNflCurrentWeek.mockRejectedValue(new Error('control table unavailable'));
    renderPage();
    await screen.findByText('frizat@example.com');
    await waitFor(() => expect(screen.getByTestId('missing-picks-1')).toHaveTextContent('Unavailable'));

    await userEvent.click(screen.getByRole('button', { name: /missing only/i }));

    expect(screen.queryByText(/no members.*missing picks/i)).not.toBeInTheDocument();
    expect(screen.getByText('frizat@example.com')).toBeInTheDocument();
    expect(screen.getByText('bob@example.com')).toBeInTheDocument();
  });
});

describe('LeaguePortalPage (frizat-e3l: mobile sizing)', () => {
  const originalMatchMedia = window.matchMedia;

  afterEach(() => {
    window.matchMedia = originalMatchMedia;
  });

  function mockViewport(matches: boolean) {
    window.matchMedia = ((query: string) => ({
      matches,
      media: query,
      onchange: null,
      addEventListener: () => {},
      removeEventListener: () => {},
      addListener: () => {},
      removeListener: () => {},
      dispatchEvent: () => false,
    })) as typeof window.matchMedia;
  }

  it('renders smaller Members-table text on a mobile viewport than on desktop', async () => {
    mockViewport(true);
    renderPage();
    const cell = await screen.findByText('frizat@example.com');

    expect(cell).toHaveStyle({ fontSize: '0.75rem' });
  });

  it('keeps the larger desktop Members-table text when not on a mobile viewport', async () => {
    mockViewport(false);
    renderPage();
    const cell = await screen.findByText('frizat@example.com');

    expect(cell).not.toHaveStyle({ fontSize: '0.75rem' });
  });

  // frizat-e3l: dropping the least-essential column is what actually gets "This Week" within
  // reach without horizontal scroll on a 390px viewport — shrinking font alone wasn't enough.
  it('drops the "Joined" column on mobile but keeps it on desktop', async () => {
    mockViewport(true);
    renderPage();
    await screen.findByText('frizat@example.com');
    expect(screen.queryByText('Joined')).not.toBeInTheDocument();

    mockViewport(false);
    renderPage();
    await screen.findAllByText('frizat@example.com');
    expect(screen.getByText('Joined')).toBeInTheDocument();
  });
});

describe('LeaguePortalPage (frizat-ndz: Members tab button grouping)', () => {
  // frizat-ndz: "Missing Only"/"Show All Members" toggling changes that button's own label
  // length, which used to shift where Invite Player/Generate Invite Link/Add User wrapped to in
  // the same flex row — reported as the whole row feeling unstable on a 390px iOS viewport. Fix
  // moves the filter toggle to its own row directly above the table, separate from the
  // member-management action buttons, which now form a stable group unaffected by the toggle.
  // jsdom has no real layout, so this asserts DOM grouping (shared Stack container), not pixel
  // position — the meaningful, testable claim is "these three share a row and the toggle doesn't."
  it('groups Invite Player, Generate Invite Link, and Add User together, separate from the Missing Only filter', async () => {
    authState.user = ADMIN_USER; // Add User only renders for admins
    sessionState.ownedLeagues = [];
    sessionState.currentLeague = null;
    mockedGetAllLeagues.mockResolvedValue([makeLeague()]);
    renderPage();
    await screen.findByText('frizat@example.com');

    const inviteButton = screen.getByRole('button', { name: 'Invite Player' });
    const generateLinkButton = screen.getByRole('button', { name: /generate invite link/i });
    const addUserButton = screen.getByRole('button', { name: /add user/i });
    const missingOnlyButton = screen.getByRole('button', { name: /missing only/i });

    const actionsRow = inviteButton.closest('.MuiStack-root');
    expect(generateLinkButton.closest('.MuiStack-root')).toBe(actionsRow);
    expect(addUserButton.closest('.MuiStack-root')).toBe(actionsRow);
    expect(missingOnlyButton.closest('.MuiStack-root')).not.toBe(actionsRow);
  });

  it('keeps the Invite Player/Generate Invite Link row stable (unaffected by the Missing Only toggle) even for a non-admin owner without Add User', async () => {
    renderPage();
    await screen.findByText('frizat@example.com');

    const inviteButton = screen.getByRole('button', { name: 'Invite Player' });
    const generateLinkButton = screen.getByRole('button', { name: /generate invite link/i });
    const actionsRowBefore = inviteButton.closest('.MuiStack-root');

    await userEvent.click(screen.getByRole('button', { name: /missing only/i }));

    expect(screen.getByRole('button', { name: 'Invite Player' }).closest('.MuiStack-root')).toBe(actionsRowBefore);
    expect(generateLinkButton.closest('.MuiStack-root')).toBe(actionsRowBefore);
  });

  // frizat-3on: the member-count Chip and the Missing Only/Show All Members button used to share
  // one flex row above the table — on a narrow (iOS ~390px) viewport the pair no longer fit on one
  // line once the button's label grew to "Show All Members", so the wrap point (and therefore the
  // button's visible position) shifted between the two toggle states. Fix: the button gets its own
  // line, never sharing a row with the Chip, so its label length can never move anything else.
  it('puts the Missing Only filter button on its own line, separate from the member-count chip', async () => {
    renderPage();
    await screen.findByText('frizat@example.com');

    // getByText resolves to the Chip's inner label span (MuiChip-label), not its root element —
    // .closest('.MuiChip-root') is required to reach the actual Chip element. Scoped to the
    // Members-tab chip specifically (text match) — the page header above also renders a cost
    // Chip via OwnerCostSummary, which a bare '.MuiChip-root' query would find first in document
    // order and falsely pass against, since it's unrelated to this row.
    const memberCountChip = screen.getByText(/members? · \$/i).closest('.MuiChip-root')!;
    const missingOnlyButton = screen.getByRole('button', { name: /missing only/i });
    const sharedContainer = missingOnlyButton.parentElement!;

    expect(memberCountChip.parentElement).toBe(sharedContainer);
    // jsdom has no real layout, so "own line" can't be checked via bounding boxes — but MUI Stack
    // renders flex-direction as plain CSS (not layout-dependent), which jsdom's getComputedStyle
    // *can* resolve. A column direction means each direct child already renders on its own line
    // by construction, with no wrap point that a label-length change could ever shift — the
    // property this test exists to guard, unlike the old `direction="row" flexWrap="wrap"` Stack
    // this replaced (frizat-3on).
    expect(getComputedStyle(sharedContainer).flexDirection).toBe('column');
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

  it('explains the difference between Invite Player and Invite Link so owners pick the right one', async () => {
    // frizat: a real incident — an owner generated a share link meaning "blast it to my whole
    // group," a member clicked it, and the owner was confused about what would happen next.
    // A short always-visible explanation next to the buttons is more likely to be read at the
    // moment of confusion than a separate help page (mobile-first: no hover-only tooltip).
    // /code-review caught that an earlier draft of this copy claimed the link joins existing
    // members instantly — stale relative to LeagueController.JoinViaLink routing an existing
    // user through the same pending-invite/Accept-Decline mechanism as Invite Player (see
    // feat/invite-link-uses-membership-banner). The real distinguishing feature is targeting
    // (one email vs. one shareable link for a whole group) — the accept/decline-vs-register
    // rule is now identical on both paths, so the copy must say so, not the opposite.
    renderPage();
    await screen.findByText('frizat@example.com');

    expect(screen.getByText(/invite player sends a request to one email.*existing members get a request to accept or decline.*new visitors register to join/i)).toBeInTheDocument();
    expect(screen.getByText(/invite link.*same way.*one shareable link for your whole group/i)).toBeInTheDocument();
  });

  it('shows the active invite link panel with copy and share buttons when a link exists', async () => {
    mockedGetCurrentInviteLink.mockResolvedValue(makeInviteLink());
    renderPage();
    await screen.findByText('frizat@example.com');

    expect(await screen.findByRole('button', { name: /^copy$/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /^share$/i })).toBeInTheDocument();
  });

  it('sharing the invite link includes the league name and an inviting message, not just a bare url', async () => {
    const originalShare = navigator.share;
    const shareMock = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'share', { value: shareMock, configurable: true });

    mockedGetCurrentInviteLink.mockResolvedValue(makeInviteLink());
    renderPage();
    await screen.findByText('frizat@example.com');

    await userEvent.click(await screen.findByRole('button', { name: /^share$/i }));

    await waitFor(() => expect(shareMock).toHaveBeenCalled());
    const shareData = shareMock.mock.calls[0][0];
    expect(shareData.text).toContain('Demo League'); // makeLeague()'s default leagueName
    expect(shareData.url).toContain('/join/');

    Object.defineProperty(navigator, 'share', { value: originalShare, configurable: true });
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

  // frizat-ndz: a link expired 1 second ago is still worth surfacing (the owner may not have
  // noticed yet) — the case above already covers that. Once it's been sitting expired for over a
  // day, showing it forever adds nothing (the owner isn't coming back to look) and just clutters
  // the page — stop rendering it at all, same as if no link had ever been generated.
  it('stops showing an expired invite link entirely once it has been expired for more than a day', async () => {
    mockedGetCurrentInviteLink.mockResolvedValue(makeInviteLink({
      expiresAt: new Date(Date.now() - 25 * 60 * 60 * 1000).toISOString(),
    }));
    renderPage();
    await screen.findByText('frizat@example.com');

    expect(screen.queryByText(/link expired/i)).not.toBeInTheDocument();
    // The button still reads "Regenerate Link" (accurate — inviteLink still exists server-side,
    // regenerating replaces it), even though the expired link's own display block is now hidden.
    expect(screen.getByRole('button', { name: /regenerate link/i })).toBeInTheDocument();
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
