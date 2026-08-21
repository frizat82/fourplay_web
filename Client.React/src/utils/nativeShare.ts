/**
 * Fires navigator.share and swallows the rejection. Cancelling the native share sheet rejects
 * with AbortError — routine, not an error — shared by every hook that calls navigator.share.
 */
export function shareViaNavigator(data: ShareData): void {
  navigator.share(data).catch(() => {});
}
