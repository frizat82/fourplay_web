import { renderHook, waitFor } from '@testing-library/react';
import { vi } from 'vitest';

vi.mock('../api/league', () => ({ getLeagueJuice: vi.fn() }));

import { getLeagueJuice } from '../api/league';
import { useLeagueMinSeason } from '../utils/useLeagueMinSeason';
import type { LeagueJuiceMappingDto } from '../types/admin';

const mockedGetLeagueJuice = vi.mocked(getLeagueJuice);

function makeJuiceMapping(season: number): LeagueJuiceMappingDto {
  return {
    id: season, leagueId: 1, leagueName: 'Demo League', season,
    juice: 13, juiceDivisional: 10, juiceConference: 6, weeklyCost: 5, dateCreated: `${season}-01-01T00:00:00Z`,
  };
}

describe('useLeagueMinSeason', () => {
  beforeEach(() => {
    mockedGetLeagueJuice.mockReset();
  });

  it('resolves to the league\'s own earliest juice-mapping season', async () => {
    mockedGetLeagueJuice.mockResolvedValue([makeJuiceMapping(2023), makeJuiceMapping(2022), makeJuiceMapping(2024)]);

    const { result } = renderHook(() => useLeagueMinSeason(1, 2020));

    await waitFor(() => expect(result.current).toBe(2022));
  });

  it('falls back to fallbackMinSeason when the league has no juice mapping yet', async () => {
    mockedGetLeagueJuice.mockResolvedValue([]);

    const { result } = renderHook(() => useLeagueMinSeason(1, 2020));

    await waitFor(() => expect(mockedGetLeagueJuice).toHaveBeenCalledWith(1));
    expect(result.current).toBe(2020);
  });

  it('falls back to fallbackMinSeason when the fetch fails', async () => {
    mockedGetLeagueJuice.mockRejectedValue(new Error('network error'));

    const { result } = renderHook(() => useLeagueMinSeason(1, 2020));

    await waitFor(() => expect(mockedGetLeagueJuice).toHaveBeenCalledWith(1));
    expect(result.current).toBe(2020);
  });

  it('returns fallbackMinSeason immediately when there is no league selected, without fetching', () => {
    const { result } = renderHook(() => useLeagueMinSeason(null, 2020));

    expect(result.current).toBe(2020);
    expect(mockedGetLeagueJuice).not.toHaveBeenCalled();
  });

  it('re-fetches when the league changes', async () => {
    mockedGetLeagueJuice.mockImplementation((leagueId: number) =>
      Promise.resolve(leagueId === 1 ? [makeJuiceMapping(2022)] : [makeJuiceMapping(2025)])
    );

    const { result, rerender } = renderHook(({ leagueId }) => useLeagueMinSeason(leagueId, 2020), {
      initialProps: { leagueId: 1 },
    });
    await waitFor(() => expect(result.current).toBe(2022));

    rerender({ leagueId: 2 });
    await waitFor(() => expect(result.current).toBe(2025));
  });
});
