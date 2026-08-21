import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { vi } from 'vitest';
import AdminLeagueCostsPage from '../pages/admin/LeagueCostsPage';
import type { AdminLeagueCostDto } from '../types/admin';

vi.mock('../api/league', () => ({ getAllLeaguesCost: vi.fn() }));
import { getAllLeaguesCost } from '../api/league';

const mockedGetAllLeaguesCost = vi.mocked(getAllLeaguesCost);

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: Infinity } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <AdminLeagueCostsPage />
    </QueryClientProvider>
  );
}

function makeCost(overrides: Partial<AdminLeagueCostDto> = {}): AdminLeagueCostDto {
  return {
    leagueId: 1,
    leagueName: 'Demo League',
    ownerUserName: 'alice',
    leagueType: 'Nfl',
    memberCount: 12,
    cost: 120,
    ...overrides,
  };
}

describe('AdminLeagueCostsPage', () => {
  beforeEach(() => {
    mockedGetAllLeaguesCost.mockReset();
  });

  it('shows each league with its owner, sport, member count, and cost, plus a total row', async () => {
    mockedGetAllLeaguesCost.mockResolvedValue([
      makeCost({ leagueId: 1, leagueName: 'NFL League', leagueType: 'Nfl', ownerUserName: 'alice', memberCount: 12, cost: 120 }),
      makeCost({ leagueId: 2, leagueName: 'CFB League', leagueType: 'Cfb', ownerUserName: 'bob', memberCount: 8, cost: 100 }),
    ]);

    renderPage();

    expect(await screen.findByText('NFL League')).toBeInTheDocument();
    expect(screen.getByText('CFB League')).toBeInTheDocument();
    expect(screen.getByText('alice')).toBeInTheDocument();
    expect(screen.getByText('bob')).toBeInTheDocument();
    expect(screen.getByText('$220')).toBeInTheDocument(); // total: 120 + 100
  });

  it('shows an empty state when there are no leagues', async () => {
    mockedGetAllLeaguesCost.mockResolvedValue([]);

    renderPage();

    expect(await screen.findByText(/no leagues/i)).toBeInTheDocument();
  });

  it('shows an error state with a retry button when the request fails', async () => {
    mockedGetAllLeaguesCost.mockRejectedValue(new Error('network error'));

    renderPage();

    expect(await screen.findByText(/couldn.t load/i)).toBeInTheDocument();
    const retryButton = screen.getByRole('button', { name: /retry/i });

    mockedGetAllLeaguesCost.mockResolvedValue([makeCost()]);
    await userEvent.click(retryButton);

    expect(await screen.findByText('Demo League')).toBeInTheDocument();
  });

  it('refetches with the newly selected season when the season selector changes', async () => {
    mockedGetAllLeaguesCost.mockResolvedValue([makeCost()]);
    renderPage();
    await screen.findByText('Demo League');

    const currentYear = new Date().getFullYear();
    await userEvent.click(screen.getByRole('combobox'));
    await userEvent.click(await screen.findByRole('option', { name: String(currentYear - 1) }));

    await waitFor(() => expect(mockedGetAllLeaguesCost).toHaveBeenCalledWith(currentYear - 1));
  });
});
