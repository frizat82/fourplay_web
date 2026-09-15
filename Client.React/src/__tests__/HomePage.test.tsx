import { render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { vi } from 'vitest';
import { useSportContext } from '../services/sport';
import HomePage from '../pages/HomePage';
import type { UserInfo } from '../types/auth';

vi.mock('../services/sport', () => ({
  useSportContext: vi.fn(() => ({ sport: 'NFL', isCfb: false, isNfl: true })),
}));

// Matches the established authState idiom used across the suite (picks.test.tsx, scores.test.tsx,
// etc.) — a plain mutable object the mock closes over, mutated per-test — rather than a vi.fn()
// spy with a full AuthContextValue shape most tests here never read.
const authState = { user: null as UserInfo | null };
vi.mock('../services/auth', () => ({ useAuth: () => authState }));

// OwnerCostSummary (rendered whenever isAuthed) reads useSession — no owned leagues by default
// so it stays a no-op (returns null) for tests that don't care about it.
vi.mock('../services/session', () => ({
  useSession: () => ({ ownedLeagues: [] }),
}));

beforeEach(() => {
  authState.user = null;
});

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <HomePage />
      </MemoryRouter>
    </QueryClientProvider>,
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

describe('HomePage — unauthenticated navigation', () => {
  // frizat: registration always requires a real invite code/link a commissioner sent — a bare
  // /account/register link (no invite params attached) was a guaranteed dead end for any
  // visitor without one already in hand. Only "Login" remains as a generic account action.
  it('has no generic Register link, only Login', () => {
    renderPage();
    expect(screen.queryByRole('link', { name: /^register$/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /register with invite/i })).not.toBeInTheDocument();
    expect(screen.getAllByRole('link', { name: /login/i }).length).toBeGreaterThan(0);
  });
});

describe('HomePage — authenticated hero CTA', () => {
  // frizat-ccm: the authenticated "Make Picks" button used SportsTennisIcon — a literal tennis
  // racket, thematically wrong for a football pick'em app on its primary post-login CTA.
  it('shows a football icon, not a tennis racket, on the "Make Picks" button', () => {
    authState.user = { userId: '1', name: 'Alice', claims: [] };
    renderPage();
    const makePicksButton = screen.getByRole('link', { name: /make picks/i });
    expect(within(makePicksButton).getByTestId('SportsFootballIcon')).toBeInTheDocument();
    expect(within(makePicksButton).queryByTestId('SportsTennisIcon')).not.toBeInTheDocument();
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
