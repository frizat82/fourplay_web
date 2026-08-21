import { toBlob } from 'html-to-image';
import { useToast } from '../services/toast';
import { shareViaNavigator } from './nativeShare';

/**
 * Renders a DOM node to a PNG and shares it as a file (native share sheet — iMessage,
 * Instagram, etc. treat it like sharing a photo). Falls back to a direct download when the
 * browser can't share files (e.g. desktop Safari) or the node isn't mounted/painted yet.
 */
export function useShareImage() {
  const toast = useToast();

  const shareImage = async (node: HTMLElement | null, title: string, fileName: string) => {
    if (!node) {
      toast.push('Nothing to share yet', 'error');
      return;
    }
    let blob: Blob | null;
    try {
      blob = await toBlob(node, { pixelRatio: 2 });
    } catch {
      blob = null;
    }
    if (!blob) {
      toast.push('Failed to generate image', 'error');
      return;
    }
    const file = new File([blob], fileName, { type: 'image/png' });

    if (navigator.canShare?.({ files: [file] })) {
      shareViaNavigator({ files: [file], title });
      return;
    }

    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    a.click();
    // Revoking immediately after click() can race with the browser actually starting to read
    // the blob (observed in Firefox), silently truncating the download — defer the revoke.
    setTimeout(() => URL.revokeObjectURL(url), 1000);
    toast.push('Image downloaded — share it from your Photos/Downloads', 'info');
  };

  return { shareImage };
}
