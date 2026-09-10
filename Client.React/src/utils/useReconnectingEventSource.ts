import { useEffect, useRef } from 'react';

const INITIAL_BACKOFF_MS = 1000;
const MAX_BACKOFF_MS = 30_000;

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
    let backoffMs = INITIAL_BACKOFF_MS;
    let stopped = false;

    const clearRetry = () => {
      if (retryTimeout) {
        clearTimeout(retryTimeout);
        retryTimeout = null;
      }
    };

    const connect = () => {
      es = new EventSource(url, { withCredentials: true });
      es.onopen = () => {
        backoffMs = INITIAL_BACKOFF_MS;
      };
      es.onmessage = () => {
        backoffMs = INITIAL_BACKOFF_MS;
        onMessageRef.current();
      };
      es.onerror = () => {
        es?.close();
        if (stopped) return;
        clearRetry();
        retryTimeout = setTimeout(() => {
          backoffMs = Math.min(backoffMs * 2, MAX_BACKOFF_MS);
          connect();
        }, backoffMs);
      };
    };

    const reconnectNow = () => {
      clearRetry();
      es?.close();
      backoffMs = INITIAL_BACKOFF_MS;
      connect();
    };

    connect();
    window.addEventListener('online', reconnectNow);

    return () => {
      stopped = true;
      clearRetry();
      es?.close();
      window.removeEventListener('online', reconnectNow);
    };
  }, [url]);
}
