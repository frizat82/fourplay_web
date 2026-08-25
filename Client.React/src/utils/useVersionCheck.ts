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

  const { data } = useQuery({
    queryKey: ['version-check'],
    queryFn: getVersion,
    enabled: !!clientSha,
    refetchInterval: (query) => {
      const latest = query.state.data;
      return latest && latest.sha !== clientSha ? false : POLL_INTERVAL_MS;
    },
    retry: false,
  });

  return { mismatch: !!data && !!clientSha && data.sha !== clientSha };
}
