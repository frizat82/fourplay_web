import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi } from 'vitest';

vi.mock('../api/auth', () => ({ requestConfirmEmail: vi.fn() }));

import ResendEmailConfirmationPage from '../pages/account/ResendEmailConfirmationPage';
import { requestConfirmEmail } from '../api/auth';
import { buildAxiosError } from './testUtils/axiosError';

describe('ResendEmailConfirmationPage', () => {
  beforeEach(() => {
    vi.mocked(requestConfirmEmail).mockResolvedValue('If your email is registered, you will receive a confirmation link.');
  });

  it('sends an absolute confirmationUrl built from window.location.origin — not a hardcoded relative path', async () => {
    // frizat: this previously sent the literal string 'Account/ConfirmEmail' — no domain, wrong
    // case vs the real /account/confirmemail route — so every resend request produced a dead link.
    render(<ResendEmailConfirmationPage />);

    await userEvent.type(screen.getByLabelText(/email/i), 'user@test.com');
    await userEvent.click(screen.getByRole('button', { name: /resend/i }));

    await waitFor(() =>
      expect(requestConfirmEmail).toHaveBeenCalledWith({
        email: 'user@test.com',
        confirmationUrl: `${window.location.origin}/account/confirmemail`,
      }),
    );
  });

  it('shows a friendly rate-limit message on a bare 429 instead of leaving an unhandled rejection', async () => {
    // This endpoint is rate-limited (Program.cs "forgot" policy) and returns a bare 429 with no
    // body — must not silently swallow the failure or render a blank/undefined message.
    vi.mocked(requestConfirmEmail).mockRejectedValue(buildAxiosError(429, ''));
    render(<ResendEmailConfirmationPage />);

    await userEvent.type(screen.getByLabelText(/email/i), 'user@test.com');
    await userEvent.click(screen.getByRole('button', { name: /resend/i }));

    await waitFor(() =>
      expect(screen.getByText('Too many attempts. Please wait a few minutes and try again.')).toBeInTheDocument(),
    );
    // Not just present — must render as an error, not the neutral "info" styling used for the
    // always-200 success response (StatusMessage previously hardcoded severity="info").
    expect(screen.getByRole('alert')).toHaveClass('MuiAlert-outlinedError');
  });

  it('shows the success message with info (not error) styling on the normal 200 response', async () => {
    render(<ResendEmailConfirmationPage />);

    await userEvent.type(screen.getByLabelText(/email/i), 'user@test.com');
    await userEvent.click(screen.getByRole('button', { name: /resend/i }));

    await waitFor(() => expect(screen.getByRole('alert')).toBeInTheDocument());
    expect(screen.getByRole('alert')).toHaveClass('MuiAlert-outlinedInfo');
  });
});
