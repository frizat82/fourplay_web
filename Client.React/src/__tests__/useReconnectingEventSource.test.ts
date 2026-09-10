import { renderHook } from '@testing-library/react';
import { vi } from 'vitest';
import { useReconnectingEventSource } from '../utils/useReconnectingEventSource';

// frizat-a2u: native EventSource's own auto-reconnect doesn't apply once .close() is called
// explicitly (ScoresPage.tsx's old `es.onerror = () => es.close()` abandoned the connection for
// the rest of the page session). This mock tracks every constructed instance so tests can assert
// a NEW EventSource gets created after a drop, not just that the old one silently stays dead.
class MockEventSource {
  static instances: MockEventSource[] = [];
  url: string;
  withCredentials: boolean;
  onopen: (() => void) | null = null;
  onmessage: (() => void) | null = null;
  onerror: (() => void) | null = null;
  closed = false;

  constructor(url: string, init?: EventSourceInit) {
    this.url = url;
    this.withCredentials = init?.withCredentials ?? false;
    MockEventSource.instances.push(this);
  }

  close() {
    this.closed = true;
  }
}

describe('useReconnectingEventSource', () => {
  beforeEach(() => {
    MockEventSource.instances = [];
    vi.stubGlobal('EventSource', MockEventSource);
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
  });

  it('does nothing when url is null', () => {
    renderHook(() => useReconnectingEventSource(null, vi.fn()));
    expect(MockEventSource.instances).toHaveLength(0);
  });

  it('opens a connection to the given url', () => {
    renderHook(() => useReconnectingEventSource('/api/live-stream', vi.fn()));
    expect(MockEventSource.instances).toHaveLength(1);
    expect(MockEventSource.instances[0].url).toBe('/api/live-stream');
    expect(MockEventSource.instances[0].withCredentials).toBe(true);
  });

  it('calls onMessage when the connection receives a message', () => {
    const onMessage = vi.fn();
    renderHook(() => useReconnectingEventSource('/api/live-stream', onMessage));
    MockEventSource.instances[0].onmessage?.();
    expect(onMessage).toHaveBeenCalledTimes(1);
  });

  it('reconnects with a bounded backoff after the connection errors, instead of abandoning it', () => {
    renderHook(() => useReconnectingEventSource('/api/live-stream', vi.fn()));
    const first = MockEventSource.instances[0];

    first.onerror?.();
    expect(first.closed).toBe(true);
    expect(MockEventSource.instances).toHaveLength(1); // not yet — backoff hasn't elapsed

    vi.advanceTimersByTime(5000);

    expect(MockEventSource.instances).toHaveLength(2);
    expect(MockEventSource.instances[1].url).toBe('/api/live-stream');
  });

  it('resets the backoff after a successful message, so a later drop retries quickly again', () => {
    renderHook(() => useReconnectingEventSource('/api/live-stream', vi.fn()));

    // First drop — let it back off and reconnect once.
    MockEventSource.instances[0].onerror?.();
    vi.advanceTimersByTime(5000);
    expect(MockEventSource.instances).toHaveLength(2);

    // A real message on the new connection should reset backoff to its initial value.
    MockEventSource.instances[1].onmessage?.();

    // Second drop — if backoff had kept growing, this wouldn't reconnect within the same window.
    MockEventSource.instances[1].onerror?.();
    vi.advanceTimersByTime(1000);
    expect(MockEventSource.instances).toHaveLength(3);
  });

  it('reconnects immediately when the browser comes back online, without waiting for backoff', () => {
    renderHook(() => useReconnectingEventSource('/api/live-stream', vi.fn()));
    expect(MockEventSource.instances).toHaveLength(1);

    window.dispatchEvent(new Event('online'));

    expect(MockEventSource.instances).toHaveLength(2);
    expect(MockEventSource.instances[0].closed).toBe(true);
  });

  it('closes the connection, clears any pending retry, and stops listening for online on unmount', () => {
    const { unmount } = renderHook(() => useReconnectingEventSource('/api/live-stream', vi.fn()));
    const first = MockEventSource.instances[0];
    first.onerror?.(); // schedule a retry that should never fire once unmounted

    unmount();

    expect(first.closed).toBe(true);
    vi.advanceTimersByTime(30_000);
    expect(MockEventSource.instances).toHaveLength(1); // the scheduled retry never ran

    window.dispatchEvent(new Event('online'));
    expect(MockEventSource.instances).toHaveLength(1); // online listener was removed too
  });

  it('tears down the old connection and opens a new one when the url changes', () => {
    const { rerender } = renderHook(
      ({ url }: { url: string | null }) => useReconnectingEventSource(url, vi.fn()),
      { initialProps: { url: '/api/live-stream' } },
    );
    expect(MockEventSource.instances).toHaveLength(1);

    rerender({ url: '/api/cfb/live-stream' });

    expect(MockEventSource.instances[0].closed).toBe(true);
    expect(MockEventSource.instances).toHaveLength(2);
    expect(MockEventSource.instances[1].url).toBe('/api/cfb/live-stream');
  });
});
