import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi } from 'vitest';
import UpdateBanner from '../components/UpdateBanner';

describe('UpdateBanner', () => {
  it('renders a snackbar with a refresh button when mismatch is true', () => {
    render(<UpdateBanner mismatch />);

    expect(screen.getByText(/new version is available/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /refresh/i })).toBeInTheDocument();
  });

  it('does not render when mismatch is false', () => {
    render(<UpdateBanner mismatch={false} />);

    expect(screen.queryByText(/new version is available/i)).not.toBeInTheDocument();
  });

  it('calls window.location.reload when the refresh button is clicked', async () => {
    const reload = vi.fn();
    vi.stubGlobal('location', { ...window.location, reload });

    render(<UpdateBanner mismatch />);
    await userEvent.click(screen.getByRole('button', { name: /refresh/i }));

    await waitFor(() => expect(reload).toHaveBeenCalledTimes(1));
    vi.unstubAllGlobals();
  });

  // On a slow reload the service worker falls back to its saved page — the old build — and the
  // banner comes straight back. Refresh drops that saved page first, so the reload waits for the
  // new one. The cached build files stay (hashed, always correct).
  it("drops the service worker's saved page, and only that, before reloading", async () => {
    const reload = vi.fn();
    vi.stubGlobal('location', { ...window.location, reload });
    const deleted: string[] = [];
    vi.stubGlobal('caches', {
      keys: vi.fn().mockResolvedValue(['ivl-shell-v1', 'ivl-assets-v1', 'other']),
      delete: vi.fn(async (key: string) => { deleted.push(key); return true; }),
    });

    render(<UpdateBanner mismatch />);
    await userEvent.click(screen.getByRole('button', { name: /refresh/i }));

    await waitFor(() => expect(reload).toHaveBeenCalledTimes(1));
    expect(deleted).toEqual(['ivl-shell-v1']);
    vi.unstubAllGlobals();
  });

  it('still reloads when the cache API fails', async () => {
    const reload = vi.fn();
    vi.stubGlobal('location', { ...window.location, reload });
    vi.stubGlobal('caches', { keys: vi.fn().mockRejectedValue(new Error('blocked')), delete: vi.fn() });

    render(<UpdateBanner mismatch />);
    await userEvent.click(screen.getByRole('button', { name: /refresh/i }));

    await waitFor(() => expect(reload).toHaveBeenCalledTimes(1));
    vi.unstubAllGlobals();
  });

  // frizat-ndz: anchored top-center with no iOS safe-area padding, this banner (including its
  // Refresh button) rendered under/behind the Dynamic Island on notched/Dynamic-Island devices —
  // unreachable exactly when a user needs to tap it. Mirrors AppLayout.test.tsx's own safe-area
  // assertion style: jsdom can't evaluate real env()/safe-area layout, so this asserts the actual
  // CSS MUI/emotion injects for the shared --safe-inset-top variable, not computed layout.
  it('offsets the Snackbar below the safe-area-inset-top (Dynamic Island / notch / status bar)', () => {
    render(<UpdateBanner mismatch />);
    const injectedCss = Array.from(document.querySelectorAll('style'))
      .map((s) => s.textContent)
      .join('\n');

    expect(injectedCss).toMatch(/-MuiSnackbar-root\{[^}]*top:calc\(var\(--safe-inset-top\)/);
  });
});
