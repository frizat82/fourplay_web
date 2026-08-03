import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { vi } from 'vitest';
import OwnerCostSummary from '../components/OwnerCostSummary';
import type { LeagueInfoDto, LeagueCostDto } from '../types/admin';

const sessionState = {
  ownedLeagues: [] as LeagueInfoDto[],
};

vi.mock('../services/session', () => ({ useSession: () => sessionState }));

vi.mock('../api/league', () => ({ getLeagueCost: vi.fn() }));
import { getLeagueCost } from '../api/league';
const mockedGetLeagueCost = vi.mocked(getLeagueCost);

function makeLeague(id: number, leagueName: string): LeagueInfoDto {
  return { id, leagueName, leagueType: 'Nfl', ownerUserId: 'me', dateCreated: '2026-06-29T00:00:00Z' };
}

function makeCost(memberCount: number, cost: number): LeagueCostDto {
  return { memberCount, cost };
}

const renderWithClient = () => {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: Infinity } } });
  return render(
    <QueryClientProvider client={client}>
      <OwnerCostSummary />
    </QueryClientProvider>,
  );
};

describe('OwnerCostSummary', () => {
  beforeEach(() => {
    mockedGetLeagueCost.mockReset();
    sessionState.ownedLeagues = [];
  });

  it('renders nothing when the user owns no leagues', async () => {
    const { container } = renderWithClient();
    await waitFor(() => expect(container).toBeEmptyDOMElement());
  });

  it('sums cost across every owned league', async () => {
    sessionState.ownedLeagues = [makeLeague(1, 'League A'), makeLeague(2, 'League B')];
    mockedGetLeagueCost.mockImplementation(async (leagueId) =>
      leagueId === 1 ? makeCost(11, 110) : makeCost(6, 100),
    );

    renderWithClient();

    await screen.findByText(/\$210/);
    expect(screen.getByText(/2 leagues/i)).toBeInTheDocument();
  });

  it('renders a single-league total without pluralizing "league"', async () => {
    sessionState.ownedLeagues = [makeLeague(1, 'League A')];
    mockedGetLeagueCost.mockResolvedValue(makeCost(5, 100));

    renderWithClient();

    await screen.findByText(/\$100/);
    expect(screen.getByText(/1 league\b/i)).toBeInTheDocument();
  });
});
