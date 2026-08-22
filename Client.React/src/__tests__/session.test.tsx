import { render, screen, waitFor } from '@testing-library/react';
import { vi } from 'vitest';
import { SessionProvider, useSession } from '../services/session';

vi.mock('../services/auth', () => ({ useAuth: () => ({ user: { userId: 'u1', name: 'Alice', claims: [] } }) }));
vi.mock('../services/sport', () => ({ useSportContext: () => ({ sport: 'NFL', isCfb: false, isNfl: true }) }));

vi.mock('../api/league', () => ({
  getLeagueUserMappingsForUser: vi.fn().mockResolvedValue([]),
  getMyLeagues: vi.fn().mockResolvedValue([]),
  getMyPendingMembershipInvites: vi.fn(),
}));

import { getMyPendingMembershipInvites } from '../api/league';
const mockedGetPending = vi.mocked(getMyPendingMembershipInvites);

function Consumer() {
  const { pendingMembershipInvites } = useSession();
  return (
    <ul>
      {pendingMembershipInvites.map((invite) => (
        <li key={invite.id}>{invite.leagueName}</li>
      ))}
    </ul>
  );
}

describe('SessionProvider — pending membership invites', () => {
  beforeEach(() => vi.clearAllMocks());

  it('fetches and exposes the logged-in user\'s pending membership invites on load', async () => {
    mockedGetPending.mockResolvedValue([
      { id: 1, leagueId: 2, leagueName: 'Demo League', invitedByUserName: 'commish', createdAt: '2026-01-01T00:00:00Z' },
    ]);

    render(
      <SessionProvider>
        <Consumer />
      </SessionProvider>
    );

    expect(await screen.findByText('Demo League')).toBeInTheDocument();
  });

  it('does not crash the session when the pending-invites fetch fails', async () => {
    mockedGetPending.mockRejectedValue(new Error('network down'));

    render(
      <SessionProvider>
        <Consumer />
      </SessionProvider>
    );

    await waitFor(() => expect(mockedGetPending).toHaveBeenCalled());
    expect(screen.queryByRole('listitem')).not.toBeInTheDocument();
  });
});
