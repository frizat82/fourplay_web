import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { vi } from 'vitest';
import { useSportContext } from '../services/sport';
import HomePage from '../pages/HomePage';

vi.mock('../services/sport', () => ({
  useSportContext: vi.fn(() => ({ sport: 'NFL', isCfb: false, isNfl: true })),
}));

vi.mock('../services/auth', () => ({
  useAuth: () => ({ user: null }),
}));

function renderPage() {
  return render(
    <MemoryRouter>
      <HomePage />
    </MemoryRouter>,
  );
}

describe('HomePage — sport indicator', () => {
  // frizat: unauthenticated visitors had no way to tell which sport a given subdomain (ivleague
  // vs cfb.ivleague) was for — the hero copy was hardcoded to NFL regardless of hostname.
  it('shows an "NFL" badge on the NFL site', () => {
    vi.mocked(useSportContext).mockReturnValue({ sport: 'NFL', isCfb: false, isNfl: true });
    renderPage();
    expect(screen.getByText('NFL')).toBeInTheDocument();
    expect(screen.queryByText('College Football')).not.toBeInTheDocument();
  });

  it('shows a "College Football" badge on the CFB site', () => {
    vi.mocked(useSportContext).mockReturnValue({ sport: 'CFB', isCfb: true, isNfl: false });
    renderPage();
    expect(screen.getByText('College Football')).toBeInTheDocument();
    expect(screen.queryByText(/^NFL$/)).not.toBeInTheDocument();
  });
});

describe('HomePage — promo video', () => {
  // frizat-f29: the video had no native controls at all, so viewers couldn't resize or
  // fullscreen it — only a custom mute button was rendered on top.
  it('renders the promo video with native controls enabled', () => {
    renderPage();
    const video = document.querySelector('video');
    expect(video).toHaveAttribute('controls');
  });
});
