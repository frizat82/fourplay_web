import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { vi } from 'vitest';

const navigateMock = vi.fn();
vi.mock('react-router-dom', async (importOriginal) => {
  const actual = await importOriginal<typeof import('react-router-dom')>();
  return { ...actual, useNavigate: () => navigateMock };
});

const { pushMock } = vi.hoisted(() => ({ pushMock: vi.fn() }));
vi.mock('../api/auth', () => ({ createUser: vi.fn() }));
vi.mock('../api/invitations', () => ({ validateInvitation: vi.fn().mockResolvedValue(null) }));
vi.mock('../services/toast', () => ({ useToast: () => ({ push: pushMock }) }));

import RegisterPage from '../pages/account/RegisterPage';
import { createUser } from '../api/auth';
import { validateInvitation } from '../api/invitations';
import { buildAxiosError } from './testUtils/axiosError';

const VALID_PASSWORD = 'Passw0rd!';

function renderWithSearch(search: string) {
  return render(
    <MemoryRouter initialEntries={[`/account/register${search}`]}>
      <RegisterPage />
    </MemoryRouter>,
  );
}

describe('RegisterPage — invite link flow', () => {
  beforeEach(() => {
    navigateMock.mockReset();
    pushMock.mockReset();
    vi.mocked(createUser).mockResolvedValue({ isSuccess: true, userId: 'new-user-1', errors: [] });
  });

  // frizat: this route renders outside AppLayout with no header/nav, and standalone PWA mode has
  // no browser chrome to fall back on — without these, a visitor who opened this from a stale
  // invite and changes their mind has no way out of the page at all.
  it('has a link back to Home and a link to Login — no dead end', async () => {
    const user = userEvent.setup();
    renderWithSearch('');

    await user.click(screen.getByRole('button', { name: /back to home/i }));
    expect(navigateMock).toHaveBeenCalledWith('/');

    await user.click(screen.getByRole('button', { name: /already have an account/i }));
    expect(navigateMock).toHaveBeenCalledWith('/account/login');
  });

  it('hides Invitation Code field when inviteLinkToken is in the URL', () => {
    renderWithSearch('?inviteLinkToken=abc123&returnUrl=/join/abc123');
    expect(screen.queryByLabelText(/invitation code/i)).not.toBeInTheDocument();
  });

  it('shows Invitation Code field when no inviteLinkToken', () => {
    renderWithSearch('');
    expect(screen.getByLabelText(/invitation code/i)).toBeInTheDocument();
  });

  it('passes inviteLinkToken to createUser when registering via link', async () => {
    renderWithSearch('?inviteLinkToken=abc123&returnUrl=/join/abc123');

    await userEvent.type(screen.getByLabelText(/username/i), 'newuser');
    await userEvent.type(screen.getByLabelText(/^email$/i), 'new@test.com');
    await userEvent.type(screen.getByLabelText(/^password$/i), VALID_PASSWORD);
    await userEvent.type(screen.getByLabelText(/confirm password/i), VALID_PASSWORD);
    await userEvent.click(screen.getByRole('button', { name: /register/i }));

    await waitFor(() =>
      expect(createUser).toHaveBeenCalledWith(
        expect.objectContaining({ inviteLinkToken: 'abc123' }),
      ),
    );
  });

  it('does NOT pass inviteLinkToken when using invitation code path', async () => {
    renderWithSearch('?inviteCode=MYCODE');

    await userEvent.type(screen.getByLabelText(/invitation code/i), 'MYCODE');
    await userEvent.type(screen.getByLabelText(/username/i), 'newuser');
    await userEvent.type(screen.getByLabelText(/^email$/i), 'new@test.com');
    await userEvent.type(screen.getByLabelText(/^password$/i), VALID_PASSWORD);
    await userEvent.type(screen.getByLabelText(/confirm password/i), VALID_PASSWORD);
    await userEvent.click(screen.getByRole('button', { name: /register/i }));

    await waitFor(() => expect(createUser).toHaveBeenCalled());
    const callArg = vi.mocked(createUser).mock.calls[0][0];
    expect(callArg.inviteLinkToken).toBeUndefined();
  });

  it('sends an absolute confirmationUrl built from window.location.origin — not a bare relative path', async () => {
    // frizat: the server no longer has a working App:BaseUrl config; the confirmation-email
    // link is now built entirely from this client-supplied absolute URL. A relative path here
    // silently breaks every new user's ability to confirm their email and log in.
    renderWithSearch('?inviteCode=MYCODE');

    await userEvent.type(screen.getByLabelText(/invitation code/i), 'MYCODE');
    await userEvent.type(screen.getByLabelText(/username/i), 'newuser');
    await userEvent.type(screen.getByLabelText(/^email$/i), 'new@test.com');
    await userEvent.type(screen.getByLabelText(/^password$/i), VALID_PASSWORD);
    await userEvent.type(screen.getByLabelText(/confirm password/i), VALID_PASSWORD);
    await userEvent.click(screen.getByRole('button', { name: /register/i }));

    await waitFor(() =>
      expect(createUser).toHaveBeenCalledWith(
        expect.objectContaining({ confirmationUrl: `${window.location.origin}/account/confirmemail` }),
      ),
    );
  });

  it('shows an error toast when the server rejects registration with a real 400 (not isSuccess:false in a 200)', async () => {
    // The real backend returns HTTP 400 (BadRequest) for a bad invite code — never 200 with
    // isSuccess:false. createUser() therefore rejects; this must not be an unhandled rejection
    // that silently does nothing (confirmed live on dev.ivleague.xyz: 3 unhandled AxiosErrors,
    // no toast, no feedback to the user at all).
    vi.mocked(createUser).mockRejectedValue(
      buildAxiosError(400, { isSuccess: false, errors: ['Invalid or expired invitation code.'] }),
    );
    renderWithSearch('?inviteCode=BADCODE');

    await userEvent.type(screen.getByLabelText(/invitation code/i), 'BADCODE');
    await userEvent.type(screen.getByLabelText(/username/i), 'newuser');
    await userEvent.type(screen.getByLabelText(/^email$/i), 'new@test.com');
    await userEvent.type(screen.getByLabelText(/^password$/i), VALID_PASSWORD);
    await userEvent.type(screen.getByLabelText(/confirm password/i), VALID_PASSWORD);
    await userEvent.click(screen.getByRole('button', { name: /register/i }));

    await waitFor(() => expect(pushMock).toHaveBeenCalledWith('Invalid or expired invitation code.', 'error'));
    expect(navigateMock).not.toHaveBeenCalled();
  });

  it('shows a friendly rate-limit toast on a bare 429 — not a blank or "undefined" message', async () => {
    // Program.cs's rate limiter ("register": 3 attempts / 5 minutes per IP) returns a bare 429
    // with no JSON body — this must not render as a blank/undefined toast.
    vi.mocked(createUser).mockRejectedValue(buildAxiosError(429, ''));
    renderWithSearch('?inviteCode=MYCODE');

    await userEvent.type(screen.getByLabelText(/invitation code/i), 'MYCODE');
    await userEvent.type(screen.getByLabelText(/username/i), 'newuser');
    await userEvent.type(screen.getByLabelText(/^email$/i), 'new@test.com');
    await userEvent.type(screen.getByLabelText(/^password$/i), VALID_PASSWORD);
    await userEvent.type(screen.getByLabelText(/confirm password/i), VALID_PASSWORD);
    await userEvent.click(screen.getByRole('button', { name: /register/i }));

    await waitFor(() =>
      expect(pushMock).toHaveBeenCalledWith('Too many attempts. Please wait a few minutes and try again.', 'error'),
    );
  });

  it('auto-fills the email field from the invitation lookup (invitation-code flow)', async () => {
    // The server already knows which email an invitation code belongs to — retyping it
    // manually risks a mismatch that silently registers the wrong address.
    vi.mocked(validateInvitation).mockResolvedValueOnce({
      email: 'invited@test.com',
      leagueName: 'Demo League',
    } as Awaited<ReturnType<typeof validateInvitation>>);
    renderWithSearch('?inviteCode=MYCODE');

    await waitFor(() => expect(screen.getByLabelText(/^email$/i)).toHaveValue('invited@test.com'));
  });

  it('shows a registering-and-joining banner (not a bare "invited" message) when the invite resolves to a league', async () => {
    // New-user registration IS joining, in one step — the copy should say so explicitly rather
    // than leaving the "what happens when I register" question unanswered.
    vi.mocked(validateInvitation).mockResolvedValueOnce({
      email: 'invited@test.com',
      leagueName: 'Demo League',
    } as Awaited<ReturnType<typeof validateInvitation>>);
    renderWithSearch('?inviteCode=MYCODE');

    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent(/registering for iv league and joining.*demo league/i));
  });

  it('does not leave an unhandled rejection when the invitation preview lookup fails', async () => {
    // Same bug class this file's other tests guard against, in the same component: a 404 for
    // a stale/expired invite code must not become an unhandled promise rejection.
    vi.mocked(validateInvitation).mockRejectedValueOnce(buildAxiosError(404));
    renderWithSearch('?inviteCode=STALECODE');

    await waitFor(() => expect(screen.getByRole('button', { name: /^register$/i })).toBeInTheDocument());
    // No league-name preview banner, and no unhandled rejection (vitest fails the test on one).
    expect(screen.queryByText(/registering for iv league and joining/i)).not.toBeInTheDocument();
  });
});
