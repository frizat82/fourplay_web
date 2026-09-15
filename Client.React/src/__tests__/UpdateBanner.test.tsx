import { render, screen } from '@testing-library/react';
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

    expect(reload).toHaveBeenCalledTimes(1);
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
