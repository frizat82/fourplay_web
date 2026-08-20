/**
 * Regression test for the logout race condition: clicking Logout must land the user on
 * the home page without needing a manual refresh. Root cause was /logout being nested
 * inside the RequireAuth-guarded route group in App.tsx — clearing auth state as part of
 * logout raced against RequireAuth's own reactive redirect to /account/login, and
 * RequireAuth sometimes won, unmounting LogoutPage mid-flight and stranding the user near
 * a login page instead of home.
 *
 * Uses the real App route tree, real AuthProvider/RequireAuth/LogoutPage — only the leaf
 * pages not under test (HomePage, LoginPage) and AppLayout's own peripheral dependencies
 * are stubbed, so this exercises the actual routing fix rather than a hand-rolled mimic.
 */
import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { vi } from 'vitest';

vi.mock('../pages/HomePage', () => ({ default: () => <div>HOME_PAGE_MARKER</div> }));
vi.mock('../pages/account/LoginPage', () => ({ default: () => <div>LOGIN_PAGE_MARKER</div> }));

vi.mock('../services/session', () => ({
  useSession: () => ({
    currentLeague: null,
    availableLeagues: [],
    selectLeague: vi.fn(),
    reloadLeagues: vi.fn(),
    clearSession: vi.fn(),
    hasNflAccess: true,
    hasCfbAccess: false,
    leaguesLoaded: true,
    ownedLeagues: [],
    pendingMembershipInvites: [],
    refreshPendingInvites: vi.fn(),
  }),
}));
vi.mock('../services/sport', () => ({ useSportContext: () => ({ sport: 'NFL', isCfb: false, isNfl: true }) }));
vi.mock('../services/theme', () => ({ useThemeMode: () => ({ mode: 'light', toggleTheme: vi.fn() }) }));
vi.mock('../services/toast', () => ({ useToast: () => ({ push: vi.fn() }) }));
vi.mock('../utils/auth', () => ({ isAdmin: () => false }));

vi.mock('../api/http', () => ({ http: { get: vi.fn(), post: vi.fn() } }));
import { http } from '../api/http';

import App from '../App';
import { AuthProvider } from '../services/auth';

const mockedHttp = http as unknown as {
  get: ReturnType<typeof vi.fn>;
  post: ReturnType<typeof vi.fn>;
};

function renderAtLogout() {
  return render(
    <MemoryRouter initialEntries={['/logout']}>
      <AuthProvider>
        <App />
      </AuthProvider>
    </MemoryRouter>,
  );
}

describe('Logout routing', () => {
  beforeEach(() => {
    mockedHttp.get.mockReset();
    mockedHttp.post.mockReset();
  });

  it('lands on the home page after logout — never stranded on a login redirect', async () => {
    mockedHttp.get.mockResolvedValue({ data: { userId: 'u1', name: 'Alice', claims: [] } });
    mockedHttp.post.mockResolvedValue({ data: { ok: true } });

    renderAtLogout();

    await waitFor(() => expect(screen.getByText('HOME_PAGE_MARKER')).toBeInTheDocument());
    expect(screen.queryByText('LOGIN_PAGE_MARKER')).not.toBeInTheDocument();
  });
});
