/**
 * Detects an installed, standalone-mode PWA (iOS "Add to Home Screen", or any platform's
 * equivalent). NFL (ivleague.xyz) and CFB (cfb.ivleague.xyz) are separate origins, so each is
 * its own installed PWA with its own scope — a cross-origin link between them always breaks out
 * of standalone mode into the regular browser, on every platform. That's a platform constraint
 * (manifest scope is origin-bound), not something a link/route change here can fix — the only
 * thing AppLayout's "Switch to CFB/NFL" control can do is warn the user before it happens.
 */
export function isStandalonePwa(): boolean {
  if (window.matchMedia?.('(display-mode: standalone)').matches) return true;
  // iOS Safari's legacy, non-standard signal — not covered by the media query above.
  return (window.navigator as Navigator & { standalone?: boolean }).standalone === true;
}
