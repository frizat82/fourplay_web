import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import ChangeUsernamePage from '../pages/account/ChangeUsernamePage';
import { vi } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import axios from 'axios';

// --- service mocks ---
const authState = {
  user: { userId: '123', name: 'testuser', claims: [] },
  refresh: vi.fn(),
};
const toastState = {
  push: vi.fn(),
};

vi.mock('../services/auth', () => ({ useAuth: () => authState }));
vi.mock('../services/toast', () => ({ useToast: () => toastState }));
vi.mock('../api/auth', () => ({ changeUsername: vi.fn() }));

import { changeUsername } from '../api/auth';
const mockedChangeUsername = vi.mocked(changeUsername);

const renderPage = () =>
  render(
    <MemoryRouter>
      <ChangeUsernamePage />
    </MemoryRouter>
  );

// extractApiErrorMessage (used by the page to surface backend error text) checks
// axios.isAxiosError — build a real-shaped rejection so that check passes in tests.
function axiosError(status: number, data: unknown) {
  return Object.assign(new Error('request failed'), {
    isAxiosError: true,
    response: { status, data },
  });
}

describe('ChangeUsernamePage', () => {
  beforeEach(() => {
    authState.refresh.mockReset();
    toastState.push.mockReset();
    mockedChangeUsername.mockReset();
  });

  it('renders the current-password and new-username fields and submit button', () => {
    renderPage();
    expect(screen.getByLabelText(/current password/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/new username/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /update username/i })).toBeInTheDocument();
  });

  it('shows validation errors when form is submitted empty', async () => {
    renderPage();
    await userEvent.click(screen.getByRole('button', { name: /update username/i }));
    await waitFor(() => {
      expect(screen.getByText(/current password is required/i)).toBeInTheDocument();
      expect(screen.getByText(/username is required/i)).toBeInTheDocument();
    });
  });

  it('shows success toast and refreshes the session (not a full logout) on success', async () => {
    mockedChangeUsername.mockResolvedValue(undefined);
    authState.refresh.mockResolvedValue(undefined);

    renderPage();
    await userEvent.type(screen.getByLabelText(/current password/i), 'Correct!1');
    await userEvent.type(screen.getByLabelText(/new username/i), 'newname');
    await userEvent.click(screen.getByRole('button', { name: /update username/i }));

    await waitFor(() => {
      expect(mockedChangeUsername).toHaveBeenCalledWith({
        currentPassword: 'Correct!1',
        newUsername: 'newname',
      });
    });
    expect(toastState.push).toHaveBeenCalledWith('Username updated', 'success');
    expect(authState.refresh).toHaveBeenCalled();
  });

  it('surfaces the server-specific message when the current password is wrong', async () => {
    mockedChangeUsername.mockRejectedValue(axiosError(400, 'Current password is incorrect.'));

    renderPage();
    await userEvent.type(screen.getByLabelText(/current password/i), 'WrongPass1!');
    await userEvent.type(screen.getByLabelText(/new username/i), 'newname');
    await userEvent.click(screen.getByRole('button', { name: /update username/i }));

    await waitFor(() => {
      expect(toastState.push).toHaveBeenCalledWith('Current password is incorrect.', 'error');
    });
    expect(authState.refresh).not.toHaveBeenCalled();
  });

  it('surfaces the server-specific message when the username is already taken', async () => {
    mockedChangeUsername.mockRejectedValue(axiosError(400, "Username 'taken' is already taken."));

    renderPage();
    await userEvent.type(screen.getByLabelText(/current password/i), 'Correct!1');
    await userEvent.type(screen.getByLabelText(/new username/i), 'taken');
    await userEvent.click(screen.getByRole('button', { name: /update username/i }));

    await waitFor(() => {
      expect(toastState.push).toHaveBeenCalledWith("Username 'taken' is already taken.", 'error');
    });
  });
});

// Sanity: confirm axios.isAxiosError recognizes our hand-built rejection shape, so the
// extractApiErrorMessage-based assertions above are testing real behavior, not a mock artifact.
it('axiosError() helper produces an object axios.isAxiosError recognizes', () => {
  expect(axios.isAxiosError(axiosError(400, 'x'))).toBe(true);
});
