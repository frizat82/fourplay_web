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
});
