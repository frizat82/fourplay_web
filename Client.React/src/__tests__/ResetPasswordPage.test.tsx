import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { vi } from 'vitest';
import { buildAxiosError } from './testUtils/axiosError';

const navigateMock = vi.fn();
vi.mock('react-router-dom', async (importOriginal) => {
  const actual = await importOriginal<typeof import('react-router-dom')>();
  return { ...actual, useNavigate: () => navigateMock };
});

vi.mock('../api/auth', () => ({ resetPassword: vi.fn() }));
const pushMock = vi.fn();
vi.mock('../services/toast', () => ({ useToast: () => ({ push: pushMock }) }));
// Bypass real base64 validation — this test only cares about the cancel link, not the code param.
vi.mock('../utils/base64', () => ({ isValidBase64Url: () => true, decodeBase64Url: () => 'token' }));

import ResetPasswordPage from '../pages/account/ResetPasswordPage';
import { resetPassword } from '../api/auth';

const VALID_PASSWORD = 'Abcdef1!';

// frizat: previously had no way to cancel/return to login before submitting.
describe('ResetPasswordPage', () => {
  beforeEach(() => {
    pushMock.mockReset();
  });

  it('has a way back to login without submitting anything', async () => {
    navigateMock.mockReset();
    render(
      <MemoryRouter initialEntries={['/account/resetpassword?code=abc']}>
        <ResetPasswordPage />
      </MemoryRouter>,
    );

    await userEvent.click(screen.getByRole('button', { name: /back to login/i }));

    expect(navigateMock).toHaveBeenCalledWith('/account/login');
    expect(resetPassword).not.toHaveBeenCalled();
  });

  // frizat: same "forgot" rate limiter as ForgotPasswordPage (Program.cs: 3 attempts/hour per IP,
  // shared between forgot-password and reset-password) — this page had the identical generic
  // "Error resetting password" catch-all with no rate-limit-specific message.
  it('shows a friendly rate-limit toast on a 429 — not the generic fallback message', async () => {
    vi.mocked(resetPassword).mockRejectedValue(buildAxiosError(429, ''));
    render(
      <MemoryRouter initialEntries={['/account/resetpassword?code=abc']}>
        <ResetPasswordPage />
      </MemoryRouter>,
    );

    await userEvent.type(screen.getByLabelText(/email/i), 'ryan3000@gmail.com');
    await userEvent.type(screen.getByLabelText(/new password/i), VALID_PASSWORD);
    await userEvent.type(screen.getByLabelText(/confirm password/i), VALID_PASSWORD);
    await userEvent.click(screen.getByRole('button', { name: /reset password/i }));

    await waitFor(() =>
      expect(pushMock).toHaveBeenCalledWith('Too many attempts. Please wait a few minutes and try again.', 'error'),
    );
  });
});
