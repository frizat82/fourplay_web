import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { vi } from 'vitest';

vi.mock('../api/league', () => ({ getAllLeagues: vi.fn() }));

import { getAllLeagues } from '../api/league';
import { useAllLeaguesMinSeason } from '../utils/useAllLeaguesMinSeason';
import type { LeagueInfoDto } from '../types/admin';

const mockedGetAllLeagues = vi.mocked(getAllLeagues);

function makeLeague(overrides: Partial<LeagueInfoDto> = {}): LeagueInfoDto {
  return {
    id: 1, leagueName: 'Demo League', dateCreated: '2024-01-01T00:00:00Z',
    ownerUserId: 'owner-1', leagueType: 'Nfl', minSeason: 2024,
    ...overrides,
  };
}

function renderWithClient(fallbackMinSeason: number) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return renderHook(() => useAllLeaguesMinSeason(fallbackMinSeason), {
    wrapper: ({ children }) => <QueryClientProvider client={client}>{children}</QueryClientProvider>,
  });
}

describe('useAllLeaguesMinSeason', () => {
  beforeEach(() => {
    mockedGetAllLeagues.mockReset();
  });

  it('resolves to the minimum minSeason across all leagues', async () => {
    mockedGetAllLeagues.mockResolvedValue([
      makeLeague({ id: 1, minSeason: 2023 }),
      makeLeague({ id: 2, minSeason: 2025 }),
      makeLeague({ id: 3, minSeason: 2022 }),
    ]);

    const { result } = renderWithClient(2020);

    await waitFor(() => expect(result.current).toBe(2022));
  });

  it('ignores leagues with no minSeason (never configured)', async () => {
    mockedGetAllLeagues.mockResolvedValue([
      makeLeague({ id: 1, minSeason: null }),
      makeLeague({ id: 2, minSeason: 2025 }),
    ]);

    const { result } = renderWithClient(2020);

    await waitFor(() => expect(result.current).toBe(2025));
  });

  it('falls back to fallbackMinSeason when no league has ever been configured', async () => {
    mockedGetAllLeagues.mockResolvedValue([makeLeague({ minSeason: null })]);

    const { result } = renderWithClient(2020);

    await waitFor(() => expect(mockedGetAllLeagues).toHaveBeenCalled());
    expect(result.current).toBe(2020);
  });

  it('falls back to fallbackMinSeason while loading', () => {
    mockedGetAllLeagues.mockReturnValue(new Promise(() => {}));

    const { result } = renderWithClient(2020);

    expect(result.current).toBe(2020);
  });

  it('falls back to fallbackMinSeason when the fetch fails', async () => {
    mockedGetAllLeagues.mockRejectedValue(new Error('network error'));

    const { result } = renderWithClient(2020);

    await waitFor(() => expect(mockedGetAllLeagues).toHaveBeenCalled());
    expect(result.current).toBe(2020);
  });
});
