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
