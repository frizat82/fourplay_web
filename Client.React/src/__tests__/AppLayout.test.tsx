/**
 * AppLayout league switcher chip tests (frizat-mon.9).
 * Verifies the league name is a tappable chip that opens a menu of leagues
 * and calls selectLeague when the user picks one.
 */
import { render, screen, fireEvent } from '@testing-library/react';
import { vi } from 'vitest';
import AppLayout from '../layouts/AppLayout';
import { MemoryRouter, Route, Routes } from 'react-router-dom';

vi.mock('../services/auth',  () => ({ useAuth: () => ({ user: { userId: 'u1', name: 'Alice', claims: [] } }) }));
vi.mock('../services/theme', () => ({ useThemeMode: () => ({ mode: 'light', toggleTheme: vi.fn() }) }));
vi.mock('../services/toast', () => ({ useToast: () => ({ push: vi.fn() }) }));
vi.mock('../utils/auth',     () => ({ isAdmin: () => false }));

const selectLeague = vi.fn();

const sessionState = {
  currentLeague: 1 as number | null,
  availableLeagues: [
    { leagueId: 1, leagueName: 'Alpha League', leagueOwnerUserId: null, userId: 'u1', userName: 'Alice', leagueType: 0, dateCreated: '' },
    { leagueId: 2, leagueName: 'Beta League',  leagueOwnerUserId: null, userId: 'u1', userName: 'Alice', leagueType: 0, dateCreated: '' },
  ],
  selectLeague,
  reloadLeagues: vi.fn(),
  clearSession: vi.fn(),
  hasNflAccess: true,
  hasCfbAccess: false,
  leaguesLoaded: true,
  ownedLeagues: [] as { id: number; leagueName: string; leagueType: string; ownerUserId: string; dateCreated: string }[],
  pendingMembershipInvites: [] as { id: number; leagueId: number; leagueName: string; invitedByUserName: string | null; createdAt: string }[],
  refreshPendingInvites: vi.fn(),
};
vi.mock('../services/session', () => ({ useSession: () => sessionState }));
vi.mock('../services/sport',   () => ({ useSportContext: () => ({ sport: 'NFL', isCfb: false, isNfl: true }) }));

function renderLayout() {
  return render(
    <MemoryRouter>
      <Routes>
        <Route path="*" element={<AppLayout />} />
      </Routes>
    </MemoryRouter>
  );
}

describe('AppLayout league switcher chip (frizat-mon.9)', () => {
  beforeEach(() => vi.clearAllMocks());

  it('renders the current league name as a clickable element in the toolbar', () => {
    renderLayout();
    expect(screen.getByRole('button', { name: /alpha league/i })).toBeInTheDocument();
  });

  it('clicking the league chip opens a menu listing available leagues', () => {
    renderLayout();
    fireEvent.click(screen.getByRole('button', { name: /alpha league/i }));
    expect(screen.getByRole('menuitem', { name: 'Alpha League' })).toBeInTheDocument();
    expect(screen.getByRole('menuitem', { name: 'Beta League' })).toBeInTheDocument();
  });

  it('selecting a different league calls selectLeague with the correct leagueId', () => {
    renderLayout();
    fireEvent.click(screen.getByRole('button', { name: /alpha league/i }));
    fireEvent.click(screen.getByRole('menuitem', { name: 'Beta League' }));
    expect(selectLeague).toHaveBeenCalledWith(2);
  });

  it('shows Select League when no current league is set', () => {
    sessionState.currentLeague = null;
    renderLayout();
    expect(screen.getByRole('button', { name: /select league/i })).toBeInTheDocument();
    sessionState.currentLeague = 1; // restore
  });
});

describe('AppLayout iOS/Android safe-area handling', () => {
  // iOS "Add to Home Screen -> Open as Web App" launches in standalone mode, rendering content
  // edge-to-edge under the status bar/Dynamic Island (env(safe-area-inset-top) evaluates to a
  // real value there, 0px in a normal browser tab). The fixed AppBar must reserve that space via
  // padding, and both spacer <Toolbar/> placeholders (drawer + main content) must grow by exactly
  // the same amount, or content directly beneath the AppBar either hides under it or leaves a gap.
  // jsdom can't evaluate real env()/safe-area layout, so this asserts the actual CSS MUI/emotion
  // injects rather than computed layout — a regression here (e.g. someone hard-coding a Toolbar
  // height instead of reusing the shared safeInsetTop constant) would go undetected by layout
  // alone anyway, since jsdom always resolves env() to nothing.
  it('pads the fixed AppBar and both spacer Toolbars by the same safe-area-inset-top value', () => {
    renderLayout();
    const injectedCss = Array.from(document.querySelectorAll('style'))
      .map((s) => s.textContent)
      .join('\n');

    expect(injectedCss).toMatch(/-MuiAppBar-root\{[^}]*padding-top:var\(--safe-inset-top\)/);
    expect(injectedCss).toMatch(/-MuiToolbar-root\{[^}]*margin-top:var\(--safe-inset-top\)/);
  });
});

describe('AppLayout header title', () => {
  // frizat: at 390px (this app's primary viewport) the hamburger + CFB-switch chip + league
  // chip + dark-mode toggle already claim most of the Toolbar's width — the "IV League" title
  // used `noWrap` with no responsive handling, so it collapsed down to an illegible "I…"
  // fragment instead of hiding cleanly. jsdom can't evaluate real container-width layout, so
  // this asserts the actual injected CSS (base display:none + a min-width media query turning
  // it back on) rather than computed layout.
  it('hides the "IV League" title below the sm breakpoint instead of letting it truncate illegibly', () => {
    renderLayout();
    const injectedCss = Array.from(document.querySelectorAll('style'))
      .map((s) => s.textContent)
      .join('\n');

    expect(injectedCss).toMatch(/@media \(min-width:0px\)\{\.css-[a-z0-9]+-MuiTypography-root\{display:none;?\}\}/);
    expect(injectedCss).toMatch(/@media \(min-width:600px\)\{\.css-[a-z0-9]+-MuiTypography-root\{display:block;?\}\}/);
  });
});
