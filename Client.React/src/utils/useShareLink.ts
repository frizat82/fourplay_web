import { useToast } from '../services/toast';
import { shareViaNavigator } from './nativeShare';

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
    if (typeof navigator.share === 'function') {
      shareViaNavigator({ title, url });
    } else {
      void copy(url);
    }
  };

  return { share, copy };
}
