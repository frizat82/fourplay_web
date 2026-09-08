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

  const share = (title: string, url: string, text?: string) => {
    if (typeof navigator.share === 'function') {
      shareViaNavigator(text ? { title, text, url } : { title, url });
    } else {
      // Without navigator.share, the only thing we can hand the user is the clipboard — so the
      // message has to travel with the url here, or it's lost entirely (this was the actual bug:
      // a browser/context lacking Web Share support silently copied the bare url with no message).
      void copy(text ? `${text} ${url}` : url);
    }
  };

  return { share, copy };
}
