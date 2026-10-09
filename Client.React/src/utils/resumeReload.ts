/** Background time after which a resume reloads instead of trusting the suspended page. */
export const RESUME_RELOAD_AFTER_MS = 10 * 60 * 1000;

/**
 * An installed iOS home-screen app left in the background for a long time can resume to an
 * all-white screen — WebKit drops the page's rendered content but resumes the suspended JS, so
 * nothing ever repaints, and only a force-quit recovered it. Reloading on resume after a long
 * background stint does what that force-quit did. A short app switch doesn't reload, so picks
 * mid-selection survive checking a text. Data is stale after this long anyway.
 */
export function createResumeReloadHandler(
  reload: () => void,
  now: () => number = Date.now,
): (state: 'hidden' | 'visible') => void {
  let hiddenAt: number | null = null;
  return state => {
    if (state === 'hidden') {
      // pagehide can follow visibilitychange for the same stint — keep the earlier timestamp.
      hiddenAt ??= now();
      return;
    }
    const wasHiddenAt = hiddenAt;
    hiddenAt = null;
    if (wasHiddenAt !== null && now() - wasHiddenAt >= RESUME_RELOAD_AFTER_MS) reload();
  };
}

export function installResumeReload(): void {
  const handle = createResumeReloadHandler(() => window.location.reload());
  document.addEventListener('visibilitychange', () =>
    handle(document.visibilityState === 'hidden' ? 'hidden' : 'visible'));
  window.addEventListener('pagehide', () => handle('hidden'));
  window.addEventListener('pageshow', () => handle('visible'));
}
