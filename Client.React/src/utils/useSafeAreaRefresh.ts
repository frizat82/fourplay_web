import { useEffect } from 'react';

/**
 * Works around an iOS WebKit bug where `env(safe-area-inset-*)` goes stale after an installed
 * standalone PWA regains foreground from a modal overlay — most reproducibly the Safari sheet
 * opened by a cross-origin link (see AppLayout's "Switch to CFB/NFL" control and utils/pwa.ts).
 * Content that depends on `--safe-inset-top` (the fixed AppBar) then renders under the Dynamic
 * Island/status bar again until something forces WebKit to recompute the env() values — toggling
 * `display` on the root element forces exactly that recalculation via a synchronous reflow.
 * A no-op everywhere `env()` doesn't apply (desktop, Android, a normal browser tab).
 */
export function useSafeAreaRefresh() {
  useEffect(() => {
    const handleVisibilityChange = () => {
      if (document.visibilityState !== 'visible') return;
      const { documentElement } = document;
      documentElement.style.display = 'none';
      void documentElement.offsetHeight; // force synchronous reflow before restoring display
      documentElement.style.display = '';
    };
    document.addEventListener('visibilitychange', handleVisibilityChange);
    return () => document.removeEventListener('visibilitychange', handleVisibilityChange);
  }, []);
}
