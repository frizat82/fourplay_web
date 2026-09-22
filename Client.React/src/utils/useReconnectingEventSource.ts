import { useEffect, useRef } from 'react';

const INITIAL_BACKOFF_MS = 1000;
const MAX_BACKOFF_MS = 30_000;
// SseHelper.cs sends a heartbeat every 28s purely to prove the connection is still alive.
// 40s gives room for one heartbeat to run a little late (network jitter) before we give up on
// the connection — comfortably more than 28s, but still well under a second missed heartbeat.
const WATCHDOG_TIMEOUT_MS = 40_000;

/**
 * Wraps EventSource with reconnect-with-backoff (frizat-a2u). Native EventSource's own
 * auto-reconnect doesn't apply once `.close()` is called explicitly — the old ScoresPage.tsx
 * code (`es.onerror = () => es.close()`) abandoned the connection for the rest of the page
 * session on any drop (network blip, laptop sleep, wifi/mobile handoff, backend restart), with
 * no visible signal to the user, silently falling back to the 5-20min poll interval — exactly
 * what "had to reload to get an update" looks like.
 *
 * Also reconnects immediately on the browser's `online` event, since a network blip alone
 * doesn't change any of the caller's gating conditions (isCurrentWeek/isPageVisible/etc.) that
 * would otherwise be needed to re-run the effect and open a fresh connection.
 *
 * A dropped connection doesn't always fire `onerror` — a proxy/NAT/load balancer can silently
 * stop delivering bytes without ever telling the browser the connection closed, in which case
 * EventSource just sits there looking "open" forever. SseHelper.cs's periodic heartbeat exists
 * to catch exactly this: it carries no score data (never calls onMessage — see below), but its
 * mere arrival resets a watchdog timer. If neither a heartbeat nor a real message shows up within
 * WATCHDOG_TIMEOUT_MS, the connection is assumed dead and force-reconnected, same as an explicit
 * error would.
 *
 * `url` is expected to already reflect the caller's own gating (pass null/undefined to disable —
 * e.g. when the page is hidden, not on the current week, or the adapter has no SSE endpoint).
 */
export function useReconnectingEventSource(url: string | null | undefined, onMessage: () => void) {
  const onMessageRef = useRef(onMessage);
  useEffect(() => {
    onMessageRef.current = onMessage;
  }, [onMessage]);

  useEffect(() => {
    if (!url) return;

    let es: EventSource | null = null;
    let retryTimeout: ReturnType<typeof setTimeout> | null = null;
    let watchdogTimeout: ReturnType<typeof setTimeout> | null = null;
    let backoffMs = INITIAL_BACKOFF_MS;
    let stopped = false;

    const clearRetry = () => {
      if (retryTimeout) {
        clearTimeout(retryTimeout);
        retryTimeout = null;
      }
    };

    const clearWatchdog = () => {
      if (watchdogTimeout) {
        clearTimeout(watchdogTimeout);
        watchdogTimeout = null;
      }
    };

    const armWatchdog = () => {
      clearWatchdog();
      watchdogTimeout = setTimeout(() => reconnectNow(), WATCHDOG_TIMEOUT_MS);
    };

    const connect = () => {
      es = new EventSource(url, { withCredentials: true });
      armWatchdog();
      es.onopen = () => {
        backoffMs = INITIAL_BACKOFF_MS;
      };
      es.onmessage = (event) => {
        backoffMs = INITIAL_BACKOFF_MS;
        armWatchdog();
        // A heartbeat only proves the connection is alive — it carries no score change and must
        // never be treated as one.
        if (event.data === 'heartbeat') return;
        onMessageRef.current();
      };
      es.onerror = () => {
        es?.close();
        if (stopped) return;
        clearRetry();
        clearWatchdog();
        retryTimeout = setTimeout(() => {
          backoffMs = Math.min(backoffMs * 2, MAX_BACKOFF_MS);
          connect();
        }, backoffMs);
      };
    };

    const reconnectNow = () => {
      clearRetry();
      clearWatchdog();
      es?.close();
      backoffMs = INITIAL_BACKOFF_MS;
      connect();
    };

    connect();
    window.addEventListener('online', reconnectNow);

    return () => {
      stopped = true;
      clearRetry();
      clearWatchdog();
      es?.close();
      window.removeEventListener('online', reconnectNow);
    };
  }, [url]);
}
