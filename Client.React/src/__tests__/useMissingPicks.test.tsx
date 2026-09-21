import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { vi } from 'vitest';
import { useMissingPicks } from '../utils/useMissingPicks';
import { pickCountsToMap } from '../services/sportAdapter';
import type { SportAdapter } from '../services/sportAdapter';

function renderWithClient<T>(hook: () => T) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return renderHook(hook, { wrapper: ({ children }) => <QueryClientProvider client={client}>{children}</QueryClientProvider> });
}

// A minimal fake adapter — the hook only ever touches .sport and .getMissingPicks, so real
// nflAdapter/cfbAdapter internals (already covered by nflAdapter.test.ts/cfbAdapter.test.ts's own
// getMissingPicks coverage) don't need to be exercised again here.
function makeAdapter(getMissingPicks: SportAdapter['getMissingPicks']): SportAdapter {
  return { sport: 'nfl', getMissingPicks } as SportAdapter;
}

// frizat-xbq: the server now returns submitted pick counts per member (never the picks
// themselves — the commissioner is also a player), so the client only maps them.
describe('pickCountsToMap', () => {
  it('keys each member\'s submitted count by userId', () => {
    const counts = pickCountsToMap([
      { userId: 'alice', pickCount: 4 },
      { userId: 'bob', pickCount: 2 },
    ]);
    expect(counts.get('alice')).toBe(4);
    expect(counts.get('bob')).toBe(2);
  });

  it('returns an empty map for no counts', () => {
    expect(pickCountsToMap([]).size).toBe(0);
  });

  it('does not include a member with no picks (caller must default missing entries to 0)', () => {
    const counts = pickCountsToMap([{ userId: 'alice', pickCount: 1 }]);
    expect(counts.has('carol')).toBe(false);
  });
});

describe('useMissingPicks', () => {
  it('resolves picksByUser and requiredPicks via adapter.getMissingPicks', async () => {
    const getMissingPicks = vi.fn().mockResolvedValue({
      picksByUser: new Map([['alice', 4], ['bob', 2]]),
      requiredPicks: 4,
    });
    const adapter = makeAdapter(getMissingPicks);

    const { result } = renderWithClient(() => useMissingPicks(adapter, 1));

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.requiredPicks).toBe(4);
    expect(result.current.picksByUser.get('alice')).toBe(4);
    expect(result.current.picksByUser.get('bob')).toBe(2);
    expect(getMissingPicks).toHaveBeenCalledWith(1);
  });

  // Off-season / no current week-slate is a real state the adapter itself resolves (e.g.
  // cfbAdapter.getMissingPicks with no current slate) — the hook must pass requiredPicks: null
  // through untouched, not treat every member as missing.
  it('passes through requiredPicks: null when the adapter resolves no current week/slate', async () => {
    const getMissingPicks = vi.fn().mockResolvedValue({ picksByUser: new Map(), requiredPicks: null });
    const adapter = makeAdapter(getMissingPicks);

    const { result } = renderWithClient(() => useMissingPicks(adapter, 1));

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.requiredPicks).toBeNull();
  });

  it('is disabled (does not fetch) when leagueId is null', () => {
    const getMissingPicks = vi.fn();
    const adapter = makeAdapter(getMissingPicks);

    renderWithClient(() => useMissingPicks(adapter, null));

    expect(getMissingPicks).not.toHaveBeenCalled();
  });

  it('is disabled (does not fetch) when enabled=false, e.g. an inactive tab', () => {
    const getMissingPicks = vi.fn();
    const adapter = makeAdapter(getMissingPicks);

    renderWithClient(() => useMissingPicks(adapter, 1, false));

    expect(getMissingPicks).not.toHaveBeenCalled();
  });

  // frizat-05h: a genuine adapter failure (e.g. NFL's control-table lookup throwing) must be
  // surfaced as isError, not silently collapsed into requiredPicks:null like a legitimate
  // off-season resolution — callers need to tell the two apart.
  it('surfaces isError when adapter.getMissingPicks rejects', async () => {
    const getMissingPicks = vi.fn().mockRejectedValue(new Error('control table unavailable'));
    const adapter = makeAdapter(getMissingPicks);

    const { result } = renderWithClient(() => useMissingPicks(adapter, 1));

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.requiredPicks).toBeNull();
  });

  it('refetch() re-invokes adapter.getMissingPicks', async () => {
    const getMissingPicks = vi.fn().mockResolvedValue({ picksByUser: new Map(), requiredPicks: 4 });
    const adapter = makeAdapter(getMissingPicks);

    const { result } = renderWithClient(() => useMissingPicks(adapter, 1));
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    getMissingPicks.mockClear();

    result.current.refetch();

    await waitFor(() => expect(getMissingPicks).toHaveBeenCalledWith(1));
  });
});
