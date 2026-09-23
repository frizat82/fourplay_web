const STORAGE_KEY = 'chunk-reload-at';
const GUARD_WINDOW_MS = 30_000;

/**
 * Recovers from a lazy route chunk that failed to load — almost always a tab opened before a
 * deploy asking for a hashed file the new deployment no longer serves. One reload lands on the
 * new build; a second failure inside the guard window is left to surface normally rather than
 * looping (the chunk is genuinely broken, or the network is down).
 */
export function handleChunkLoadError(
  event: { preventDefault: () => void },
  reload: () => void = () => window.location.reload(),
  now: number = Date.now(),
): void {
  let last = 0;
  try {
    last = Number(sessionStorage.getItem(STORAGE_KEY)) || 0;
  } catch { /* storage blocked — fall through with no guard history */ }
  if (now - last < GUARD_WINDOW_MS) return;

  try {
    sessionStorage.setItem(STORAGE_KEY, String(now));
  } catch {
    // Without storage there's no loop guard, so don't risk an infinite reload.
    return;
  }
  event.preventDefault();
  reload();
}

export function installChunkReloadGuard(): void {
  window.addEventListener('vite:preloadError', e => handleChunkLoadError(e));
}
