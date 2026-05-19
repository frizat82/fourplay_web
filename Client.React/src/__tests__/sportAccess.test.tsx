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

vi.mock('../services/auth',  () => ({ useAuth: () => ({ user: { userId: 'u1', name: 'Alice', claims: [] } }) }));
vi.mock('../services/theme', () => ({ useThemeMode: () => ({ mode: 'light', toggleTheme: vi.fn() }) }));
vi.mock('../services/toast', () => ({ useToast: () => ({ push: vi.fn() }) }));
vi.mock('../utils/auth',     () => ({ isAdmin: () => false }));

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
};
vi.mock('../services/session', () => ({ useSession: () => sessionState }));

// Mutable sport context
const sportContext = { sport: 'NFL', isCfb: false, isNfl: true };
vi.mock('../services/sport', () => ({ useSportContext: () => sportContext }));

function renderLayout() {
  return render(
    <MemoryRouter>
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
    Object.assign(sessionState, { currentLeague: 1, hasNflAccess: true, hasCfbAccess: true, leaguesLoaded: true });
    Object.assign(sportContext, { sport: 'NFL', isCfb: false, isNfl: true });
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
});
