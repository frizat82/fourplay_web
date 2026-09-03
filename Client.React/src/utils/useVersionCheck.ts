import { useEffect, useRef } from 'react';
import { useQuery } from '@tanstack/react-query';
import { getVersion } from '../api/version';

const POLL_INTERVAL_MS = 5 * 60 * 1000;

/**
 * Polls /api/version and compares it against the client build's own baked-in SHA
 * (VITE_APP_VERSION, injected at build time — see vite.config.ts). Once a mismatch is observed,
 * refetchInterval reads it straight from the query's own cached state and returns `false`, so
 * polling stops on its own — the cached (mismatched) result is what keeps `mismatch` true for the
 * rest of the session, with no separate latch ref/state needed. VITE_APP_VERSION is unset in
 * local dev, so the check is a no-op there.
 */
export function useVersionCheck() {
  const clientSha = import.meta.env.VITE_APP_VERSION;

  const { data, refetch } = useQuery({
    queryKey: ['version-check'],
    queryFn: getVersion,
    enabled: !!clientSha,
    refetchInterval: (query) => {
      const latest = query.state.data;
      return latest && latest.sha !== clientSha ? false : POLL_INTERVAL_MS;
    },
    retry: false,
  });

  const mismatch = !!data && !!clientSha && data.sha !== clientSha;

  // refetchInterval above only runs while the document has focus — TanStack Query's default. An
  // installed iOS (or Android) home-screen web app almost never gets a real page reload on
  // reopen; it resumes the same suspended session instead, so a build backgrounded across a
  // deploy can miss every scheduled poll tick and never surface the update banner. Force an
  // immediate check the moment the app becomes visible again rather than waiting up to
  // POLL_INTERVAL_MS for the next tick. Read `mismatch` via a ref, not the effect's own closure,
  // so the listener doesn't need to be torn down and re-added on every fetch.
  const mismatchRef = useRef(mismatch);
  useEffect(() => {
    mismatchRef.current = mismatch;
  }, [mismatch]);

  useEffect(() => {
    if (!clientSha) return;
    const handleVisibilityChange = () => {
      if (document.visibilityState === 'visible' && !mismatchRef.current) {
        void refetch();
      }
    };
    document.addEventListener('visibilitychange', handleVisibilityChange);
    return () => document.removeEventListener('visibilitychange', handleVisibilityChange);
  }, [clientSha, refetch]);

  return { mismatch };
}
