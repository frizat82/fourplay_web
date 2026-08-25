import { act, renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { vi } from 'vitest';

vi.mock('../api/version', () => ({ getVersion: vi.fn() }));

import { getVersion } from '../api/version';
import { useVersionCheck } from '../utils/useVersionCheck';

const mockedGetVersion = vi.mocked(getVersion);

function renderWithClient() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: Infinity } } });
  return renderHook(() => useVersionCheck(), {
    wrapper: ({ children }) => <QueryClientProvider client={client}>{children}</QueryClientProvider>,
  });
}

describe('useVersionCheck', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.stubEnv('VITE_APP_VERSION', 'client-sha-123');
  });

  afterEach(() => {
    vi.unstubAllEnvs();
  });

  it('shows no banner when client SHA matches server SHA', async () => {
    mockedGetVersion.mockResolvedValue({ sha: 'client-sha-123', env: 'Production', timestamp: '2026-01-01T00:00:00Z' });

    const { result } = renderWithClient();

    await waitFor(() => expect(mockedGetVersion).toHaveBeenCalled());
    expect(result.current.mismatch).toBe(false);
  });

  it('sets mismatch flag when server SHA differs from client SHA', async () => {
    mockedGetVersion.mockResolvedValue({ sha: 'server-sha-456', env: 'Production', timestamp: '2026-01-01T00:00:00Z' });

    const { result } = renderWithClient();

    await waitFor(() => expect(result.current.mismatch).toBe(true));
  });

  it('does not re-trigger after mismatch already detected', async () => {
    mockedGetVersion.mockResolvedValue({ sha: 'server-sha-456', env: 'Production', timestamp: '2026-01-01T00:00:00Z' });

    const { result } = renderWithClient();
    await waitFor(() => expect(result.current.mismatch).toBe(true));

    const callsAfterFirstMismatch = mockedGetVersion.mock.calls.length;

    // Advance past several more poll intervals (5 min each) — a real mismatch already
    // latched should not keep hammering the endpoint or flip state again.
    vi.useFakeTimers();
    await act(async () => {
      await vi.advanceTimersByTimeAsync(5 * 60 * 1000 * 3);
    });
    vi.useRealTimers();

    expect(result.current.mismatch).toBe(true);
    expect(mockedGetVersion.mock.calls.length).toBe(callsAfterFirstMismatch);
  });
});
