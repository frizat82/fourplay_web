import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { vi } from 'vitest';

vi.mock('../api/league', () => ({
  getNflCurrentWeek: vi.fn(),
  getLeaguePicks: vi.fn(),
}));
vi.mock('../api/cfb', () => ({
  getCfbCurrentSlate: vi.fn(),
  getCfbAllPicks: vi.fn(),
}));

import { getNflCurrentWeek, getLeaguePicks } from '../api/league';
import { getCfbCurrentSlate, getCfbAllPicks } from '../api/cfb';
import { countPicksByUser, useMissingPicks } from '../utils/useMissingPicks';
import type { NflPickDto } from '../types/picks';
import type { CfbPickDto, CfbSlateDto } from '../types/league';

const mockedGetNflCurrentWeek = vi.mocked(getNflCurrentWeek);
const mockedGetLeaguePicks = vi.mocked(getLeaguePicks);
const mockedGetCfbCurrentSlate = vi.mocked(getCfbCurrentSlate);
const mockedGetCfbAllPicks = vi.mocked(getCfbAllPicks);

function makeNflPick(userId: string, overrides: Partial<NflPickDto> = {}): NflPickDto {
  return { id: 1, leagueId: 1, userId, userName: userId, team: 'KC', pick: 'Spread', nflWeek: 2, season: 2026, dateCreated: '', ...overrides };
}

function makeCfbPick(userId: string, overrides: Partial<CfbPickDto> = {}): CfbPickDto {
  return { id: 1, userId, userName: userId, leagueId: 1, cfbSlateId: 1, team: 'IU', pickType: 'Spread', season: 2026, ...overrides };
}

function makeSlate(overrides: Partial<CfbSlateDto> = {}): CfbSlateDto {
  return { id: 1, season: 2026, slateNumber: 3, label: 'Week 3', slateType: 'RegularSeason', startDate: '', endDate: '', ...overrides };
}

function renderWithClient<T>(hook: () => T) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return renderHook(hook, { wrapper: ({ children }) => <QueryClientProvider client={client}>{children}</QueryClientProvider> });
}

describe('countPicksByUser', () => {
  it('counts multiple picks per user (e.g. one spread pick per game) without double-counting users', () => {
    const counts = countPicksByUser([
      { userId: 'alice' }, { userId: 'alice' }, { userId: 'alice' }, { userId: 'alice' },
      { userId: 'bob' }, { userId: 'bob' },
    ]);
    expect(counts.get('alice')).toBe(4);
    expect(counts.get('bob')).toBe(2);
  });

  it('returns an empty map for no picks', () => {
    expect(countPicksByUser([]).size).toBe(0);
  });

  it('does not include a user who made zero picks (caller must default missing entries to 0)', () => {
    const counts = countPicksByUser([{ userId: 'alice' }]);
    expect(counts.has('carol')).toBe(false);
  });
});

describe('useMissingPicks — NFL', () => {
  beforeEach(() => vi.clearAllMocks());

  it('resolves picksByUser and requiredPicks for the current NFL week', async () => {
    mockedGetNflCurrentWeek.mockResolvedValue({
      weekId: 2, season: 2026, isPostSeason: false, weekLabel: 'Week 2', scoringFormat: 'Standard', spreadLockDatetime: '',
    });
    mockedGetLeaguePicks.mockResolvedValue([
      makeNflPick('alice'), makeNflPick('alice'), makeNflPick('alice'), makeNflPick('alice'),
      makeNflPick('bob'), makeNflPick('bob'),
    ]);

    const { result } = renderWithClient(() => useMissingPicks(1, false));

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.requiredPicks).toBe(4);
    expect(result.current.picksByUser.get('alice')).toBe(4);
    expect(result.current.picksByUser.get('bob')).toBe(2);
    expect(mockedGetLeaguePicks).toHaveBeenCalledWith(1, 2026, 2);
  });

  it('is disabled (does not fetch) when leagueId is null', () => {
    renderWithClient(() => useMissingPicks(null, false));
    expect(mockedGetNflCurrentWeek).not.toHaveBeenCalled();
  });
});

describe('useMissingPicks — CFB', () => {
  beforeEach(() => vi.clearAllMocks());

  it('resolves picksByUser and requiredPicks for the current CFB slate', async () => {
    mockedGetCfbCurrentSlate.mockResolvedValue(makeSlate({ slateNumber: 3 }));
    mockedGetCfbAllPicks.mockResolvedValue([makeCfbPick('alice'), makeCfbPick('alice')]);

    const { result } = renderWithClient(() => useMissingPicks(1, true));

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.requiredPicks).toBe(4);
    expect(result.current.picksByUser.get('alice')).toBe(2);
    expect(mockedGetCfbAllPicks).toHaveBeenCalledWith(1, 1);
  });

  // Off-season / no slate row yet is a real state (e.g. between seasons) — must not be treated
  // as "everyone is missing their picks."
  it('returns requiredPicks: null when there is no current CFB slate', async () => {
    mockedGetCfbCurrentSlate.mockResolvedValue(null);

    const { result } = renderWithClient(() => useMissingPicks(1, true));

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.requiredPicks).toBeNull();
    expect(mockedGetCfbAllPicks).not.toHaveBeenCalled();
  });
});
