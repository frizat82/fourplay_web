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

vi.mock('../api/auth', () => ({ forgotPassword: vi.fn() }));
const pushMock = vi.fn();
vi.mock('../services/toast', () => ({ useToast: () => ({ push: pushMock }) }));

import ForgotPasswordPage from '../pages/account/ForgotPasswordPage';
import { forgotPassword } from '../api/auth';

// frizat: previously had no way to cancel/return to login before submitting.
describe('ForgotPasswordPage', () => {
  beforeEach(() => {
    pushMock.mockReset();
  });

  it('has a way back to login without submitting anything', async () => {
    navigateMock.mockReset();
    render(
      <MemoryRouter>
        <ForgotPasswordPage />
      </MemoryRouter>,
    );

    await userEvent.click(screen.getByRole('button', { name: /back to login/i }));

    expect(navigateMock).toHaveBeenCalledWith('/account/login');
    expect(forgotPassword).not.toHaveBeenCalled();
  });

  // frizat: "forgot" rate limiter (Program.cs: 3 attempts/hour per IP, shared with reset-password)
  // returns a bare 429 — the page previously swallowed every failure into the same generic
  // "Error requesting password reset" toast, giving a genuinely rate-limited user (who retried
  // after not seeing the email land) no indication to stop retrying and just wait.
  it('shows a friendly rate-limit toast on a 429 — not the generic fallback message', async () => {
    vi.mocked(forgotPassword).mockRejectedValue(buildAxiosError(429, ''));
    render(
      <MemoryRouter>
        <ForgotPasswordPage />
      </MemoryRouter>,
    );

    await userEvent.type(screen.getByLabelText(/email/i), 'ryan3000@gmail.com');
    await userEvent.click(screen.getByRole('button', { name: /reset password/i }));

    await waitFor(() =>
      expect(pushMock).toHaveBeenCalledWith('Too many attempts. Please wait a few minutes and try again.', 'error'),
    );
  });
});
