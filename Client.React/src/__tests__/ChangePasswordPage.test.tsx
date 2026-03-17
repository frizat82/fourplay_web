import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import ChangePasswordPage from '../pages/account/ChangePasswordPage';
import { vi } from 'vitest';
import { MemoryRouter } from 'react-router-dom';

// --- service mocks ---
const authState = {
  user: { userId: '123', name: 'testuser@example.com', claims: [] },
  logout: vi.fn(),
};
const sessionState = {
  currentLeague: 1 as number | null,
  availableLeagues: [],
  selectLeague: vi.fn(),
  reloadLeagues: vi.fn(),
  clearSession: vi.fn(),
};
const toastState = {
  push: vi.fn(),
};

vi.mock('../services/auth', () => ({ useAuth: () => authState }));
vi.mock('../services/session', () => ({ useSession: () => sessionState }));
vi.mock('../services/toast', () => ({ useToast: () => toastState }));
vi.mock('../api/auth', () => ({ changePassword: vi.fn() }));

import { changePassword } from '../api/auth';
const mockedChangePassword = vi.mocked(changePassword);

const renderPage = () =>
  render(
    <MemoryRouter>
      <ChangePasswordPage />
    </MemoryRouter>
  );

describe('ChangePasswordPage', () => {
  beforeEach(() => {
    authState.logout.mockReset();
    sessionState.clearSession.mockReset();
    toastState.push.mockReset();
    mockedChangePassword.mockReset();
  });

  it('renders all three password fields and submit button', () => {
    renderPage();
    expect(screen.getByLabelText(/old password/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/new password/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/confirm password/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /update password/i })).toBeInTheDocument();
  });

  it('shows validation errors when form is submitted empty', async () => {
    renderPage();
    await userEvent.click(screen.getByRole('button', { name: /update password/i }));
    await waitFor(() => {
      expect(screen.getByText(/current password is required/i)).toBeInTheDocument();
    });
  });

  it('shows error when new password does not meet complexity requirements', async () => {
    renderPage();
    await userEvent.type(screen.getByLabelText(/old password/i), 'OldPass1!');
    await userEvent.type(screen.getByLabelText(/new password/i), 'short');
    await userEvent.type(screen.getByLabelText(/confirm password/i), 'short');
    await userEvent.click(screen.getByRole('button', { name: /update password/i }));
    await waitFor(() => {
      expect(screen.getByText(/at least 6 characters/i)).toBeInTheDocument();
    });
  });

  it('shows error when new password and confirm password do not match', async () => {
    renderPage();
    await userEvent.type(screen.getByLabelText(/old password/i), 'OldPass1!');
    await userEvent.type(screen.getByLabelText(/new password/i), 'NewPass1!');
    await userEvent.type(screen.getByLabelText(/confirm password/i), 'Different1!');
    await userEvent.click(screen.getByRole('button', { name: /update password/i }));
    await waitFor(() => {
      expect(
        screen.getByText(/the new password and confirmation password do not match/i)
      ).toBeInTheDocument();
    });
  });

  it('shows success toast, calls logout and navigates on successful change', async () => {
    mockedChangePassword.mockResolvedValue(undefined);
    authState.logout.mockResolvedValue(undefined);

    renderPage();
    await userEvent.type(screen.getByLabelText(/old password/i), 'OldPass1!');
    await userEvent.type(screen.getByLabelText(/new password/i), 'NewPass1!');
    await userEvent.type(screen.getByLabelText(/confirm password/i), 'NewPass1!');
    await userEvent.click(screen.getByRole('button', { name: /update password/i }));

    await waitFor(() => {
      expect(mockedChangePassword).toHaveBeenCalledWith({
        email: 'testuser@example.com',
        currentPassword: 'OldPass1!',
        password: 'NewPass1!',
      });
    });
    expect(toastState.push).toHaveBeenCalledWith('Password updated', 'success');
    expect(authState.logout).toHaveBeenCalled();
    expect(sessionState.clearSession).toHaveBeenCalled();
  });

  it('shows error toast when API call fails', async () => {
    mockedChangePassword.mockRejectedValue(new Error('Server error'));

    renderPage();
    await userEvent.type(screen.getByLabelText(/old password/i), 'OldPass1!');
    await userEvent.type(screen.getByLabelText(/new password/i), 'NewPass1!');
    await userEvent.type(screen.getByLabelText(/confirm password/i), 'NewPass1!');
    await userEvent.click(screen.getByRole('button', { name: /update password/i }));

    await waitFor(() => {
      expect(toastState.push).toHaveBeenCalledWith('Error updating password', 'error');
    });
  });

  it('shows error toast when user is not logged in', async () => {
    const savedUser = authState.user;
    // @ts-expect-error — intentionally null for test
    authState.user = null;

    renderPage();
    await userEvent.type(screen.getByLabelText(/old password/i), 'OldPass1!');
    await userEvent.type(screen.getByLabelText(/new password/i), 'NewPass1!');
    await userEvent.type(screen.getByLabelText(/confirm password/i), 'NewPass1!');
    await userEvent.click(screen.getByRole('button', { name: /update password/i }));

    await waitFor(() => {
      expect(toastState.push).toHaveBeenCalledWith('User not found', 'error');
    });
    // restore
    authState.user = savedUser;
  });
});
