import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi } from 'vitest';
import PendingInviteBanner from '../components/PendingInviteBanner';
import type { PendingMembershipInviteDto } from '../api/league';

vi.mock('../api/league', () => ({
  acceptMembershipInvite: vi.fn(),
  declineMembershipInvite: vi.fn(),
}));

const toastPush = vi.fn();
vi.mock('../services/toast', () => ({ useToast: () => ({ push: toastPush }) }));

const refreshPendingInvites = vi.fn();
const reloadLeagues = vi.fn();
vi.mock('../services/session', () => ({
  useSession: () => ({
    pendingMembershipInvites: mockInvites,
    refreshPendingInvites,
    reloadLeagues,
  }),
}));

import { acceptMembershipInvite, declineMembershipInvite } from '../api/league';
const mockedAccept = vi.mocked(acceptMembershipInvite);
const mockedDecline = vi.mocked(declineMembershipInvite);

let mockInvites: PendingMembershipInviteDto[] = [];

function makeInvite(overrides: Partial<PendingMembershipInviteDto> = {}): PendingMembershipInviteDto {
  return {
    id: 1,
    leagueId: 10,
    leagueName: 'Demo League',
    invitedByUserName: 'commish',
    createdAt: '2026-01-01T00:00:00Z',
    ...overrides,
  };
}

describe('PendingInviteBanner', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockInvites = [];
    refreshPendingInvites.mockResolvedValue(undefined);
    reloadLeagues.mockResolvedValue(undefined);
  });

  it('renders nothing when there are no pending invites', () => {
    mockInvites = [];
    const { container } = render(<PendingInviteBanner />);
    expect(container).toBeEmptyDOMElement();
  });

  it('shows "You\'re being asked to join {League}" with Accept/Decline for each pending invite', () => {
    mockInvites = [makeInvite({ leagueName: 'Demo League' })];
    render(<PendingInviteBanner />);
    expect(screen.getByRole('alert')).toHaveTextContent(/you're being asked to join.*demo league/i);
    expect(screen.getByRole('button', { name: /accept/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /decline/i })).toBeInTheDocument();
  });

  it('clicking Accept calls the accept endpoint and refreshes leagues + invites', async () => {
    mockInvites = [makeInvite({ id: 7 })];
    mockedAccept.mockResolvedValue(undefined);
    render(<PendingInviteBanner />);

    await userEvent.click(screen.getByRole('button', { name: /accept/i }));

    await waitFor(() => expect(mockedAccept).toHaveBeenCalledWith(7));
    expect(reloadLeagues).toHaveBeenCalled();
    expect(refreshPendingInvites).toHaveBeenCalled();
  });

  it('clicking Decline calls the decline endpoint and refreshes invites, without reloading leagues', async () => {
    mockInvites = [makeInvite({ id: 7 })];
    mockedDecline.mockResolvedValue(undefined);
    render(<PendingInviteBanner />);

    await userEvent.click(screen.getByRole('button', { name: /decline/i }));

    await waitFor(() => expect(mockedDecline).toHaveBeenCalledWith(7));
    expect(refreshPendingInvites).toHaveBeenCalled();
    expect(reloadLeagues).not.toHaveBeenCalled();
  });

  it('shows an error toast when Accept fails, without crashing', async () => {
    mockInvites = [makeInvite({ id: 7 })];
    mockedAccept.mockRejectedValue(new Error('network down'));
    render(<PendingInviteBanner />);

    await userEvent.click(screen.getByRole('button', { name: /accept/i }));

    await waitFor(() => expect(toastPush).toHaveBeenCalledWith(expect.stringMatching(/fail/i), 'error'));
  });

  it('does not show a "Failed to accept" error when Accept succeeds but the follow-up refresh fails', async () => {
    // The accept itself already succeeded server-side — a flaky refresh afterward must not be
    // reported as an accept failure, which would wrongly tell the user nothing happened.
    mockInvites = [makeInvite({ id: 7 })];
    mockedAccept.mockResolvedValue(undefined);
    reloadLeagues.mockRejectedValue(new Error('network down'));
    render(<PendingInviteBanner />);

    await userEvent.click(screen.getByRole('button', { name: /accept/i }));

    await waitFor(() => expect(mockedAccept).toHaveBeenCalledWith(7));
    expect(toastPush).not.toHaveBeenCalled();
  });

  it('does not show a "Failed to decline" error when Decline succeeds but the follow-up refresh fails', async () => {
    mockInvites = [makeInvite({ id: 7 })];
    mockedDecline.mockResolvedValue(undefined);
    refreshPendingInvites.mockRejectedValue(new Error('network down'));
    render(<PendingInviteBanner />);

    await userEvent.click(screen.getByRole('button', { name: /decline/i }));

    await waitFor(() => expect(mockedDecline).toHaveBeenCalledWith(7));
    expect(toastPush).not.toHaveBeenCalled();
  });
});
