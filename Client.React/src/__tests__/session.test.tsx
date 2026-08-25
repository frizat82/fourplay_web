import { render, screen, waitFor } from '@testing-library/react';
import { vi } from 'vitest';
import { SessionProvider, useSession } from '../services/session';

const mockSport = vi.hoisted(() => ({ sport: 'NFL' as 'NFL' | 'CFB' }));

vi.mock('../services/auth', () => ({ useAuth: () => ({ user: { userId: 'u1', name: 'Alice', claims: [] } }) }));
vi.mock('../services/sport', () => ({ useSportContext: () => ({ sport: mockSport.sport, isCfb: mockSport.sport === 'CFB', isNfl: mockSport.sport === 'NFL' }) }));

vi.mock('../api/league', () => ({
  getLeagueUserMappingsForUser: vi.fn().mockResolvedValue([]),
  getMyLeagues: vi.fn().mockResolvedValue([]),
  getMyPendingMembershipInvites: vi.fn(),
}));

import { getLeagueUserMappingsForUser, getMyLeagues, getMyPendingMembershipInvites } from '../api/league';
const mockedGetPending = vi.mocked(getMyPendingMembershipInvites);
const mockedGetMappings = vi.mocked(getLeagueUserMappingsForUser);
const mockedGetMyLeagues = vi.mocked(getMyLeagues);

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

function LeaguesConsumer() {
  const { ownedLeagues, availableLeagues, leaguesLoaded } = useSession();
  if (!leaguesLoaded) return <span>loading</span>;
  return (
    <div>
      <span data-testid="owned-count">{ownedLeagues.length}</span>
      <ul>
        {availableLeagues.map((l) => (
          <li key={l.leagueId}>{l.leagueName}</li>
        ))}
      </ul>
    </div>
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

describe('SessionProvider — league ownership + sport filtering', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockSport.sport = 'NFL';
  });

  it('ownedLeagues is non-empty when the user owns a league in the current sport', async () => {
    mockedGetMyLeagues.mockResolvedValue([
      { id: 1, leagueName: 'My NFL League', dateCreated: '2026-01-01T00:00:00Z', ownerUserId: 'u1', leagueType: 'Nfl' },
    ]);

    render(
      <SessionProvider>
        <LeaguesConsumer />
      </SessionProvider>
    );

    expect(await screen.findByTestId('owned-count')).toHaveTextContent('1');
  });

  it('ownedLeagues is empty when the user owns no leagues', async () => {
    mockedGetMyLeagues.mockResolvedValue([]);

    render(
      <SessionProvider>
        <LeaguesConsumer />
      </SessionProvider>
    );

    expect(await screen.findByTestId('owned-count')).toHaveTextContent('0');
  });

  it('sport filter excludes CFB leagues on the NFL subdomain', async () => {
    mockSport.sport = 'NFL';
    mockedGetMappings.mockResolvedValue([
      { id: 1, leagueId: 10, userId: 'u1', userName: 'Alice', leagueName: 'NFL League', leagueType: 0, dateCreated: '2026-01-01T00:00:00Z' },
      { id: 2, leagueId: 20, userId: 'u1', userName: 'Alice', leagueName: 'CFB League', leagueType: 1, dateCreated: '2026-01-01T00:00:00Z' },
    ]);

    render(
      <SessionProvider>
        <LeaguesConsumer />
      </SessionProvider>
    );

    expect(await screen.findByText('NFL League')).toBeInTheDocument();
    expect(screen.queryByText('CFB League')).not.toBeInTheDocument();
  });

  it('sport filter excludes NFL leagues on the CFB subdomain', async () => {
    mockSport.sport = 'CFB';
    mockedGetMappings.mockResolvedValue([
      { id: 1, leagueId: 10, userId: 'u1', userName: 'Alice', leagueName: 'NFL League', leagueType: 0, dateCreated: '2026-01-01T00:00:00Z' },
      { id: 2, leagueId: 20, userId: 'u1', userName: 'Alice', leagueName: 'CFB League', leagueType: 1, dateCreated: '2026-01-01T00:00:00Z' },
    ]);

    render(
      <SessionProvider>
        <LeaguesConsumer />
      </SessionProvider>
    );

    expect(await screen.findByText('CFB League')).toBeInTheDocument();
    expect(screen.queryByText('NFL League')).not.toBeInTheDocument();
  });
});
