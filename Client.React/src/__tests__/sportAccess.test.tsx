/**
 * Sport access control tests.
 * Three scenarios:
 *  1. NFL-only user on CFB site → sees "No CFB access" message + link to NFL
 *  2. CFB-only user on NFL site → sees "No NFL access" message + link to CFB
 *  3. User with both → no access message on either site
 */
import { render, screen } from '@testing-library/react';
import { vi } from 'vitest';
import AppLayout from '../layouts/AppLayout';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { isAdmin } from '../utils/auth';

const mockedIsAdmin = vi.mocked(isAdmin);

vi.mock('../services/auth',  () => ({ useAuth: () => ({ user: { userId: 'u1', name: 'Alice', claims: [] } }) }));
vi.mock('../services/theme', () => ({ useThemeMode: () => ({ mode: 'light', toggleTheme: vi.fn() }) }));
vi.mock('../services/toast', () => ({ useToast: () => ({ push: vi.fn() }) }));
vi.mock('../utils/auth',     () => ({ isAdmin: vi.fn() }));

// Mutable session state — updated per test
const sessionState = {
  currentLeague: 1 as number | null,
  availableLeagues: [{ leagueId: 1, leagueName: 'Demo', leagueOwnerUserId: null, userId: 'u1', userName: 'Alice', leagueType: 0, dateCreated: '' }],
  selectLeague: vi.fn(),
  reloadLeagues: vi.fn(),
  clearSession: vi.fn(),
  hasNflAccess: true,
  hasCfbAccess: true,
  leaguesLoaded: true,
  ownedLeagues: [] as { id: number; leagueName: string; leagueType: string; ownerUserId: string; dateCreated: string }[],
  pendingMembershipInvites: [] as { id: number; leagueId: number; leagueName: string; invitedByUserName: string | null; createdAt: string }[],
  refreshPendingInvites: vi.fn(),
};
vi.mock('../services/session', () => ({ useSession: () => sessionState }));

// Mutable sport context
const sportContext = { sport: 'NFL', isCfb: false, isNfl: true };
vi.mock('../services/sport', () => ({ useSportContext: () => sportContext }));

function renderLayout(initialPath = '/') {
  return render(
    <MemoryRouter initialEntries={[initialPath]}>
      <Routes>
        <Route path="*" element={<AppLayout />} />
      </Routes>
    </MemoryRouter>
  );
}

describe('Sport access control', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    // Reset to defaults
    Object.assign(sessionState, { currentLeague: 1, hasNflAccess: true, hasCfbAccess: true, leaguesLoaded: true, ownedLeagues: [] });
    Object.assign(sportContext, { sport: 'NFL', isCfb: false, isNfl: true });
    mockedIsAdmin.mockReturnValue(false);
  });

  it('NFL-only user on NFL site — no access message, renders nav normally', () => {
    sessionState.hasCfbAccess = false;
    renderLayout();
    expect(screen.queryByText(/No.*access/i)).not.toBeInTheDocument();
    expect(screen.getByText('IV League')).toBeInTheDocument();
  });

  it('NFL-only user on CFB site — shows No CFB access with Go to NFL link', () => {
    Object.assign(sportContext, { sport: 'CFB', isCfb: true, isNfl: false });
    Object.assign(sessionState, { hasNflAccess: true, hasCfbAccess: false, currentLeague: null });
    renderLayout();
    expect(screen.getByText(/No CFB access/i)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Go to NFL/i })).toBeInTheDocument();
  });

  // frizat: League Portal is how a user gets access to a sport in the first place (self-serve
  // Create League, frizat-d6l) — the blanket "no access" gate previously replaced every routed
  // page including this one, so an NFL-only user could never reach League Portal on the CFB
  // site to create their first CFB league at all. Every other sport-specific page (Picks,
  // Scores, Leaderboard) still needs the gate, since those genuinely require an existing league.
  it('NFL-only user on CFB site visiting League Portal — no access block, reaches the page', () => {
    Object.assign(sportContext, { sport: 'CFB', isCfb: true, isNfl: false });
    Object.assign(sessionState, { hasNflAccess: true, hasCfbAccess: false, currentLeague: null });
    renderLayout('/league/manage');
    expect(screen.queryByText(/No CFB access/i)).not.toBeInTheDocument();
  });

  // frizat: an admin's own personal league membership is unrelated to whether they should be able
  // to reach the admin panel or manage the platform on a given sport's site — the block is meant
  // for regular users who genuinely have nothing to look at, not for admins doing platform work.
  it('admin with zero CFB access reaches the CFB admin panel directly — no access block at all', () => {
    mockedIsAdmin.mockReturnValue(true);
    Object.assign(sportContext, { sport: 'CFB', isCfb: true, isNfl: false });
    Object.assign(sessionState, { hasNflAccess: true, hasCfbAccess: false, currentLeague: null });
    renderLayout('/admin/jobManager');
    expect(screen.queryByText(/No CFB access/i)).not.toBeInTheDocument();
  });

  it('admin with zero CFB access reaches ordinary sport-specific pages too (Scores), not just admin/League Portal', () => {
    mockedIsAdmin.mockReturnValue(true);
    Object.assign(sportContext, { sport: 'CFB', isCfb: true, isNfl: false });
    Object.assign(sessionState, { hasNflAccess: true, hasCfbAccess: false, currentLeague: null });
    renderLayout('/scores');
    expect(screen.queryByText(/No CFB access/i)).not.toBeInTheDocument();
  });

  it('non-admin with zero CFB access still sees the block on the same admin route — the exemption is admin-only', () => {
    mockedIsAdmin.mockReturnValue(false);
    Object.assign(sportContext, { sport: 'CFB', isCfb: true, isNfl: false });
    Object.assign(sessionState, { hasNflAccess: true, hasCfbAccess: false, currentLeague: null });
    renderLayout('/admin/jobManager');
    expect(screen.getByText(/No CFB access/i)).toBeInTheDocument();
  });

  it('CFB-only user on NFL site — shows No NFL access with Go to CFB link', () => {
    Object.assign(sessionState, { hasNflAccess: false, hasCfbAccess: true, currentLeague: null });
    renderLayout();
    expect(screen.getByText(/No NFL access/i)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Go to CFB/i })).toBeInTheDocument();
  });

  it('User with both sports on NFL site — no access message', () => {
    renderLayout();
    expect(screen.queryByText(/No.*access/i)).not.toBeInTheDocument();
  });

  it('User with both sports on CFB site — no access message', () => {
    Object.assign(sportContext, { sport: 'CFB', isCfb: true, isNfl: false });
    sessionState.currentLeague = 2;
    renderLayout();
    expect(screen.queryByText(/No.*access/i)).not.toBeInTheDocument();
  });

  it('User with no access to either sport — shows generic no access, no Go to link', () => {
    Object.assign(sessionState, { hasNflAccess: false, hasCfbAccess: false, currentLeague: null });
    renderLayout();
    expect(screen.getByText(/No.*access/i)).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Go to/i })).not.toBeInTheDocument();
  });

  // Self-serve league creation (frizat-d6l): any authenticated user can create their own
  // league, so the nav link must stay visible whether or not they own one yet.
  it('My Leagues nav link is visible even when the user owns no leagues', () => {
    sessionState.ownedLeagues = [];
    renderLayout();
    expect(screen.getByRole('link', { name: /my leagues/i })).toBeInTheDocument();
  });

  it('My Leagues nav link is visible when the user owns a league', () => {
    sessionState.ownedLeagues = [{ id: 1, leagueName: 'Demo', leagueType: 'Nfl', ownerUserId: 'u1', dateCreated: '' }];
    renderLayout();
    expect(screen.getByRole('link', { name: /my leagues/i })).toBeInTheDocument();
  });

  // Top-level NFL/CFB quick-switch (frizat: previously only reachable via the buried
  // no-access empty-state button) — always visible in the toolbar when the user has access
  // to the other sport, distinct from the empty-state "Go to {sport} site" wording so both
  // can coexist without ambiguous accessible names.
  describe('Top-level sport quick-switch', () => {
    it('shows a Switch-to-CFB toolbar link on the NFL site when the user has CFB access too', () => {
      renderLayout();
      expect(screen.getByRole('link', { name: /switch to cfb/i })).toBeInTheDocument();
    });

    it('shows a Switch-to-NFL toolbar link on the CFB site when the user has NFL access too', () => {
      Object.assign(sportContext, { sport: 'CFB', isCfb: true, isNfl: false });
      sessionState.currentLeague = 2;
      renderLayout();
      expect(screen.getByRole('link', { name: /switch to nfl/i })).toBeInTheDocument();
    });

    it('hides the toolbar quick-switch when the user has no access to the other sport', () => {
      sessionState.hasCfbAccess = false;
      renderLayout();
      expect(screen.queryByRole('link', { name: /switch to cfb/i })).not.toBeInTheDocument();
    });

    it('does not duplicate: hidden when the empty-state "Go to {sport} site" button is already showing for the same link', () => {
      // hasOther=true (empty state offers a switch) AND hasCurrent=false (empty state actually
      // renders) is exactly the scenario where the toolbar chip and the empty-state button would
      // otherwise both point at the same URL with different wording.
      Object.assign(sportContext, { sport: 'CFB', isCfb: true, isNfl: false });
      Object.assign(sessionState, { hasNflAccess: true, hasCfbAccess: false, currentLeague: null });
      renderLayout();
      expect(screen.getByRole('link', { name: /go to nfl/i })).toBeInTheDocument();
      expect(screen.queryByRole('link', { name: /switch to nfl/i })).not.toBeInTheDocument();
    });
  });

  // frizat: tapping "Switch to CFB" from an installed iOS/Android PWA always drops the user into
  // the system browser, out of standalone mode — a platform constraint (each sport is a separate
  // origin, so a separate installed PWA, and neither iOS nor Android lets a plain web link hand
  // off to a different origin's installed app) that no routing change or in-app dialog can fix.
  // The control is only ever shown to users who already have access to both sports (`hasOther`),
  // so hiding it in standalone mode doesn't hide the other sport's existence from anyone — it just
  // removes a control whose only possible action is an unexpected app-to-browser jump.
  describe('Switch-sport controls inside an installed standalone PWA', () => {
    let originalMatchMedia: typeof window.matchMedia;

    beforeEach(() => {
      originalMatchMedia = window.matchMedia;
      window.matchMedia = ((query: string) => ({
        matches: query === '(display-mode: standalone)',
        media: query,
        onchange: null,
        addEventListener: () => {},
        removeEventListener: () => {},
        addListener: () => {},
        removeListener: () => {},
        dispatchEvent: () => false,
      })) as typeof window.matchMedia;
    });

    afterEach(() => {
      window.matchMedia = originalMatchMedia;
    });

    it('hides the toolbar quick-switch control entirely when running as a standalone PWA', () => {
      renderLayout();
      expect(screen.queryByRole('link', { name: /switch to cfb/i })).not.toBeInTheDocument();
    });

    // /code-review: unlike the toolbar chip, this button is the ONLY link on the "no access"
    // empty-state screen — hiding it in standalone mode would strand a user who installed the
    // wrong sport's PWA with no way out. It stays visible in every mode.
    it('still shows the empty-state "Go to {sport} site" button when running as a standalone PWA', () => {
      Object.assign(sportContext, { sport: 'CFB', isCfb: true, isNfl: false });
      Object.assign(sessionState, { hasNflAccess: true, hasCfbAccess: false, currentLeague: null });
      renderLayout();
      expect(screen.getByRole('link', { name: /go to nfl/i })).toBeInTheDocument();
    });

    it('still shows the toolbar quick-switch link when not running as a standalone PWA', () => {
      window.matchMedia = originalMatchMedia;
      renderLayout();
      expect(screen.getByRole('link', { name: /^switch to cfb site$/i })).toBeInTheDocument();
    });
  });
});
