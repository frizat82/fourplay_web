import { render, screen, waitFor, act } from '@testing-library/react';
import ConfirmEmailPage from '../pages/account/ConfirmEmailPage';
import { vi } from 'vitest';
import { MemoryRouter, Route, Routes } from 'react-router-dom';

vi.mock('../api/auth', () => ({ confirmEmail: vi.fn() }));

import { confirmEmail } from '../api/auth';
const mockedConfirmEmail = vi.mocked(confirmEmail);

// A minimal valid base64url string — "hello" encoded
const VALID_CODE = 'aGVsbG8';

const renderPage = (search = '') =>
  render(
    <MemoryRouter initialEntries={[`/account/confirm-email${search}`]}>
      <Routes>
        <Route path="/account/confirm-email" element={<ConfirmEmailPage />} />
        <Route path="/" element={<div>Home</div>} />
        <Route path="/account/login" element={<div>Login</div>} />
      </Routes>
    </MemoryRouter>
  );

describe('ConfirmEmailPage', () => {
  beforeEach(() => {
    mockedConfirmEmail.mockReset();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('shows loading state initially before async effect resolves', () => {
    // Use a never-resolving promise so the component stays in loading state
    mockedConfirmEmail.mockImplementation(() => new Promise(() => {}));

    renderPage(`?userId=abc&code=${VALID_CODE}`);

    expect(screen.getByText(/confirming your email/i)).toBeInTheDocument();
  });

  it('shows success message when token is valid', async () => {
    mockedConfirmEmail.mockResolvedValue(undefined);

    renderPage(`?userId=abc&code=${VALID_CODE}`);

    await waitFor(() => {
      expect(
        screen.getByText(/thank you for confirming your email/i)
      ).toBeInTheDocument();
    });
  });

  it('hides loading text after successful confirmation', async () => {
    mockedConfirmEmail.mockResolvedValue(undefined);

    renderPage(`?userId=abc&code=${VALID_CODE}`);

    // Loading text disappears once state resolves
    await waitFor(() => {
      expect(screen.queryByText(/^Confirming your email\.\.\.$/i)).not.toBeInTheDocument();
    });
  });

  it('shows error alert when API call fails', async () => {
    mockedConfirmEmail.mockRejectedValue(new Error('Token expired'));

    renderPage(`?userId=abc&code=${VALID_CODE}`);

    await waitFor(() => {
      expect(screen.getByText(/error confirming your email/i)).toBeInTheDocument();
    });
    // Verify it renders as an alert role
    const alert = screen.getByRole('alert');
    expect(alert).toBeInTheDocument();
  });

  it('shows error when userId param is missing', async () => {
    renderPage(`?code=${VALID_CODE}`);

    await waitFor(() => {
      expect(screen.getByText(/invalid confirmation link/i)).toBeInTheDocument();
    });
    // confirmEmail should never be called
    expect(mockedConfirmEmail).not.toHaveBeenCalled();
  });

  it('shows error when code param is missing', async () => {
    renderPage('?userId=abc');

    await waitFor(() => {
      expect(screen.getByText(/invalid confirmation link/i)).toBeInTheDocument();
    });
    expect(mockedConfirmEmail).not.toHaveBeenCalled();
  });

  it('handles missing URL params entirely (no query string)', async () => {
    renderPage('');

    await waitFor(() => {
      expect(screen.getByText(/invalid confirmation link/i)).toBeInTheDocument();
    });
    expect(mockedConfirmEmail).not.toHaveBeenCalled();
  });

  it('shows error for invalid base64url code format', async () => {
    // "!!!" is not a valid base64url string — atob will throw
    renderPage('?userId=abc&code=!!!');

    await waitFor(() => {
      expect(screen.getByText(/invalid confirmation code format/i)).toBeInTheDocument();
    });
    expect(mockedConfirmEmail).not.toHaveBeenCalled();
  });

  it('navigates to login after successful confirmation (after 3s timeout)', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    mockedConfirmEmail.mockResolvedValue(undefined);

    renderPage(`?userId=abc&code=${VALID_CODE}`);

    // Flush microtasks/promises so the async effect resolves
    await act(async () => {
      await vi.runAllTimersAsync();
    });

    // Trigger the 3-second setTimeout redirect
    await act(async () => {
      vi.advanceTimersByTime(3000);
      await vi.runAllTimersAsync();
    });

    expect(screen.getByText('Login')).toBeInTheDocument();

    vi.useRealTimers();
  });

  it('navigates to home after invalid link redirect (after 3s timeout)', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });

    renderPage('');

    // Flush the useEffect synchronous branch (no awaiting needed for sync path)
    await act(async () => {
      await vi.runAllTimersAsync();
    });

    await act(async () => {
      vi.advanceTimersByTime(3000);
      await vi.runAllTimersAsync();
    });

    expect(screen.getByText('Home')).toBeInTheDocument();

    vi.useRealTimers();
  });

  it('displays the Email Confirmation heading', () => {
    mockedConfirmEmail.mockImplementation(() => new Promise(() => {}));
    renderPage(`?userId=abc&code=${VALID_CODE}`);
    expect(screen.getByText(/email confirmation/i)).toBeInTheDocument();
  });
});
