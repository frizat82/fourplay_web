import { useToast } from '../services/toast';

/**
 * Native-share-with-clipboard-fallback, shared by every "Share"/"Copy" button in the app.
 * Mirrors the pattern originally written for the league invite link.
 */
export function useShareLink() {
  const toast = useToast();

  const copy = async (url: string) => {
    try {
      await navigator.clipboard.writeText(url);
      toast.push('Link copied', 'info');
    } catch {
      toast.push('Failed to copy link', 'error');
    }
  };

  const share = (title: string, url: string) => {
    if (navigator.share) {
      // Cancelling the native share sheet rejects with AbortError — routine, not an error.
      navigator.share({ title, url }).catch(() => {});
    } else {
      void copy(url);
    }
  };

  return { share, copy };
}
