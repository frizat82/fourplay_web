import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi } from 'vitest';
import AdminInvitationsPage from '../pages/admin/InvitationsPage';
import type { LeagueInfoDto } from '../types/admin';

vi.mock('../services/auth', () => ({
  useAuth: () => ({ user: { userId: 'admin-1', name: 'Admin', claims: [{ type: 'role', value: 'Administrator' }] } }),
}));
const toastPush = vi.fn();
vi.mock('../services/toast', () => ({ useToast: () => ({ push: toastPush }) }));

vi.mock('../api/invitations', () => ({
  getAllInvitations: vi.fn().mockResolvedValue([]),
  createInvitation: vi.fn(),
  deleteInvitation: vi.fn(),
  resendInvitation: vi.fn(),
}));

vi.mock('../api/league', () => ({ getAllLeagues: vi.fn() }));
import { getAllLeagues } from '../api/league';
import { getAllInvitations, createInvitation, resendInvitation } from '../api/invitations';
import type { InvitationDto } from '../types/admin';

const mockedGetAllLeagues = vi.mocked(getAllLeagues);
const mockedGetAllInvitations = vi.mocked(getAllInvitations);
const mockedCreateInvitation = vi.mocked(createInvitation);
const mockedResendInvitation = vi.mocked(resendInvitation);

function makeLeague(overrides: Partial<LeagueInfoDto> = {}): LeagueInfoDto {
  return {
    id: 1,
    leagueName: 'Demo League',
    leagueType: 'Nfl',
    ownerUserId: 'someone-else',
    dateCreated: '2026-06-29T00:00:00Z',
    ...overrides,
  };
}

function makeInvitation(overrides: Partial<InvitationDto> = {}): InvitationDto {
  return {
    id: 1,
    invitationCode: 'code-abc',
    email: 'invite@example.com',
    createdAt: '2026-06-29T00:00:00Z',
    isUsed: false,
    isExpired: false,
    isValid: true,
    ...overrides,
  };
}

beforeEach(() => {
  toastPush.mockClear();
  mockedGetAllLeagues.mockResolvedValue([]);
  mockedGetAllInvitations.mockResolvedValue([]);
  mockedCreateInvitation.mockResolvedValue({ email: 'newplayer@example.com', outcome: 'NewUserInvitationSent' });
});

describe('AdminInvitationsPage', () => {
  it('lists leagues the admin does not personally belong to, across sports', async () => {
    mockedGetAllLeagues.mockResolvedValue([
      makeLeague({ id: 1, leagueName: 'Demo League', leagueType: 'Nfl' }),
      makeLeague({ id: 2, leagueName: 'CFB Demo League', leagueType: 'Cfb' }),
    ]);

    render(<AdminInvitationsPage />);

    await userEvent.click(await screen.findByRole('combobox'));
    expect(await screen.findByRole('option', { name: /CFB Demo League/i })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: /Demo League \(Nfl\)/i })).toBeInTheDocument();
  });

  it('creating an invitation does not make any separate email-sending call — the backend sends it', async () => {
    render(<AdminInvitationsPage />);

    await userEvent.type(await screen.findByLabelText(/email address/i), 'newplayer@example.com');
    await userEvent.click(screen.getByRole('button', { name: /^invite$/i }));

    expect(mockedCreateInvitation).toHaveBeenCalledWith('newplayer@example.com', 'admin-1', null);
    expect(mockedResendInvitation).not.toHaveBeenCalled();
  });

  it('tells the admin an already-registered invitee will see an in-app accept/decline request, not an email', async () => {
    // Regression: this admin tool used to always create a registration-email Invitation, even
    // for an email that already had an account — the invitee got nothing (no email needed, no
    // banner ever created). The backend now detects this and creates a pending membership
    // invite instead; the toast must reflect that so the admin isn't misled into thinking an
    // email went out.
    mockedCreateInvitation.mockResolvedValue({ email: 'existing@example.com', outcome: 'ExistingUserInvitePending' });
    render(<AdminInvitationsPage />);

    await userEvent.type(await screen.findByLabelText(/email address/i), 'existing@example.com');
    await userEvent.click(screen.getByRole('button', { name: /^invite$/i }));

    await waitFor(() =>
      expect(toastPush).toHaveBeenCalledWith(expect.stringMatching(/already has an account/i), 'success')
    );
  });

  it('resend button re-sends the invitation email for an existing invitation', async () => {
    mockedGetAllInvitations.mockResolvedValue([makeInvitation({ email: 'pending@example.com' })]);

    render(<AdminInvitationsPage />);

    await userEvent.click(await screen.findByRole('button', { name: /resend invitation to pending@example.com/i }));

    expect(mockedResendInvitation).toHaveBeenCalledWith(1);
  });

  it('shows "Pending Confirmation" for a used invitation whose registered user has not confirmed their email', async () => {
    // This is exactly what confused an admin twice: a green "Used" chip read as "fully
    // onboarded" when the registered user was actually still stuck unable to log in.
    mockedGetAllInvitations.mockResolvedValue([
      makeInvitation({ email: 'stuck@example.com', isUsed: true, registeredUserEmailConfirmed: false }),
    ]);

    render(<AdminInvitationsPage />);

    expect(await screen.findByText('Pending Confirmation')).toBeInTheDocument();
    // "Used" is also a separate stats-card label elsewhere on the page — scope to the row.
    expect(screen.queryByRole('row', { name: /stuck@example\.com.*used/i })).not.toBeInTheDocument();
  });

  it('shows "Confirmed" for a used invitation whose registered user has confirmed their email', async () => {
    mockedGetAllInvitations.mockResolvedValue([
      makeInvitation({ email: 'done@example.com', isUsed: true, registeredUserEmailConfirmed: true }),
    ]);

    render(<AdminInvitationsPage />);

    expect(await screen.findByText('Confirmed')).toBeInTheDocument();
  });
});
