import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { vi } from 'vitest';
import AdminLeagueCostsPage from '../pages/admin/LeagueCostsPage';
import type { AdminLeagueCostDto } from '../types/admin';

vi.mock('../api/league', () => ({ getAllLeaguesCost: vi.fn(), getAllLeagues: vi.fn() }));
import { getAllLeaguesCost, getAllLeagues } from '../api/league';
import type { LeagueInfoDto } from '../types/admin';

const mockedGetAllLeaguesCost = vi.mocked(getAllLeaguesCost);
const mockedGetAllLeagues = vi.mocked(getAllLeagues);

function makeLeagueInfo(overrides: Partial<LeagueInfoDto> = {}): LeagueInfoDto {
  return {
    id: 1,
    leagueName: 'Demo League',
    dateCreated: '2024-01-01T00:00:00Z',
    ownerUserId: 'owner-1',
    leagueType: 'Nfl',
    minSeason: 2024,
    ...overrides,
  };
}

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
    mockedGetAllLeagues.mockReset();
    mockedGetAllLeagues.mockResolvedValue([makeLeagueInfo()]);
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

  it('does not offer a season before the earliest season any league has been configured for', async () => {
    mockedGetAllLeagues.mockResolvedValue([makeLeagueInfo({ minSeason: 2025 })]);
    mockedGetAllLeaguesCost.mockResolvedValue([makeCost()]);
    renderPage();
    await screen.findByText('Demo League');

    await userEvent.click(screen.getByRole('combobox'));
    expect(await screen.findByRole('option', { name: '2025' })).toBeInTheDocument();
    expect(screen.queryByRole('option', { name: '2024' })).toBeNull();
  });

  it('renders a sane fallback season range while the leagues list is still loading', async () => {
    // getAllLeagues never resolves during this test — the page must still render usable season
    // options (the fallback floor) rather than crashing or showing an empty select.
    mockedGetAllLeagues.mockReturnValue(new Promise(() => {}));
    mockedGetAllLeaguesCost.mockResolvedValue([makeCost()]);
    renderPage();
    await screen.findByText('Demo League');

    await userEvent.click(screen.getByRole('combobox'));
    const currentYear = new Date().getFullYear();
    expect(await screen.findByRole('option', { name: String(currentYear) })).toBeInTheDocument();
  });

  it('clamps the selected season up to the earliest available one when every league is newer than the current year', async () => {
    // Every league's minSeason is in the future relative to CURRENT_YEAR (e.g. only next
    // season's leagues have been configured so far) — the initial `useState(CURRENT_YEAR)`
    // value falls outside the computed season range, so the page must derive an in-range season
    // rather than leaving the Select controlled to a value with no matching option.
    const currentYear = new Date().getFullYear();
    const futureSeason = currentYear + 5;
    mockedGetAllLeagues.mockResolvedValue([makeLeagueInfo({ minSeason: futureSeason })]);
    mockedGetAllLeaguesCost.mockResolvedValue([makeCost()]);
    renderPage();

    await waitFor(() => expect(mockedGetAllLeaguesCost).toHaveBeenCalledWith(futureSeason));
    expect(await screen.findByText(String(futureSeason))).toBeInTheDocument();
  });
});
