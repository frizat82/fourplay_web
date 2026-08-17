import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router';
import { vi } from 'vitest';
import JoinLeaguePage from '../pages/JoinLeaguePage';

const navigateMock = vi.fn();

vi.mock('react-router', async (importOriginal) => {
  const actual = await importOriginal<typeof import('react-router')>();
  return { ...actual, useNavigate: () => navigateMock };
});

const authState: { user: { userId: string; name: string; claims: unknown[] } | null } = {
  user: null,
};
vi.mock('../services/auth', () => ({ useAuth: () => authState }));

const sessionMock = { reloadLeagues: vi.fn().mockResolvedValue(undefined) };
vi.mock('../services/session', () => ({ useSession: () => sessionMock }));

vi.mock('../api/league', () => ({
  validateInviteLink: vi.fn(),
  joinViaLink: vi.fn(),
}));

import { validateInviteLink, joinViaLink } from '../api/league';

function renderWithToken(token: string) {
  return render(
    <MemoryRouter initialEntries={[`/join/${token}`]}>
      <Routes>
        <Route path="/join/:token" element={<JoinLeaguePage />} />
      </Routes>
    </MemoryRouter>,
  );
}

describe('JoinLeaguePage', () => {
  beforeEach(() => {
    navigateMock.mockReset();
    sessionMock.reloadLeagues.mockResolvedValue(undefined);
    authState.user = null;
  });

  it('shows error when token is invalid or expired', async () => {
    vi.mocked(validateInviteLink).mockResolvedValue(null);
    renderWithToken('badtoken');
    await waitFor(() =>
      expect(screen.getByText(/expired or invalid/i)).toBeInTheDocument()
    );
  });

  it('shows league name and sign-up CTA when logged out and token is valid', async () => {
    vi.mocked(validateInviteLink).mockResolvedValue({
      token: 'tok123',
      leagueId: 1,
      leagueName: 'My NFL League',
      expiresAt: new Date(Date.now() + 3600000).toISOString(),
    });
    authState.user = null;
    renderWithToken('tok123');
    await waitFor(() => expect(screen.getByText('My NFL League')).toBeInTheDocument());
    expect(screen.getByRole('button', { name: /create an account/i })).toBeInTheDocument();
  });

  it('navigates to register page when sign-up CTA is clicked', async () => {
    vi.mocked(validateInviteLink).mockResolvedValue({
      token: 'tok123',
      leagueId: 1,
      leagueName: 'My NFL League',
      expiresAt: new Date(Date.now() + 3600000).toISOString(),
    });
    authState.user = null;
    renderWithToken('tok123');
    await waitFor(() => screen.getByRole('button', { name: /create an account/i }));
    await userEvent.click(screen.getByRole('button', { name: /create an account/i }));
    expect(navigateMock).toHaveBeenCalledWith(
      expect.stringMatching(/\/account\/register\?inviteLinkToken=tok123&returnUrl=.*join.*tok123/),
    );
  });

  it('shows Join button when user is logged in and token is valid', async () => {
    vi.mocked(validateInviteLink).mockResolvedValue({
      token: 'tok123',
      leagueId: 1,
      leagueName: 'My NFL League',
      expiresAt: new Date(Date.now() + 3600000).toISOString(),
    });
    authState.user = { userId: 'user-1', name: 'Alice', claims: [] };
    renderWithToken('tok123');
    await waitFor(() => expect(screen.getByRole('button', { name: /join/i })).toBeInTheDocument());
  });

  it('calls joinViaLink and navigates to dashboard on success', async () => {
    vi.mocked(validateInviteLink).mockResolvedValue({
      token: 'tok123',
      leagueId: 1,
      leagueName: 'My NFL League',
      expiresAt: new Date(Date.now() + 3600000).toISOString(),
    });
    vi.mocked(joinViaLink).mockResolvedValue(undefined);
    authState.user = { userId: 'user-1', name: 'Alice', claims: [] };
    renderWithToken('tok123');
    await waitFor(() => screen.getByRole('button', { name: /join/i }));
    await userEvent.click(screen.getByRole('button', { name: /join/i }));
    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith('/dashboard'));
    expect(sessionMock.reloadLeagues).toHaveBeenCalled();
  });
});
