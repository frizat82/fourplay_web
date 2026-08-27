import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { vi } from 'vitest';

const navigateMock = vi.fn();
vi.mock('react-router-dom', async (importOriginal) => {
  const actual = await importOriginal<typeof import('react-router-dom')>();
  return { ...actual, useNavigate: () => navigateMock };
});

vi.mock('../api/auth', () => ({ requestConfirmEmail: vi.fn() }));

import ResendEmailConfirmationPage from '../pages/account/ResendEmailConfirmationPage';
import { requestConfirmEmail } from '../api/auth';
import { buildAxiosError } from './testUtils/axiosError';

function renderResend(search = '') {
  return render(
    <MemoryRouter initialEntries={[`/account/resendemailconfirmation${search}`]}>
      <ResendEmailConfirmationPage />
    </MemoryRouter>,
  );
}

describe('ResendEmailConfirmationPage', () => {
  beforeEach(() => {
    navigateMock.mockReset();
    vi.mocked(requestConfirmEmail).mockResolvedValue('If your email is registered, you will receive a confirmation link.');
  });

  // frizat: this route renders outside AppLayout with no header/nav — without this, a visitor
  // who didn't want to resend anything had no way out of the page at all.
  it('has a way back to login without resending anything', async () => {
    renderResend();

    await userEvent.click(screen.getByRole('button', { name: /back to login/i }));

    expect(navigateMock).toHaveBeenCalledWith('/account/login');
    expect(requestConfirmEmail).not.toHaveBeenCalled();
  });

  it('sends an absolute confirmationUrl built from window.location.origin — not a hardcoded relative path', async () => {
    // frizat: this previously sent the literal string 'Account/ConfirmEmail' — no domain, wrong
    // case vs the real /account/confirmemail route — so every resend request produced a dead link.
    renderResend();

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
    renderResend();

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
    renderResend();

    await userEvent.type(screen.getByLabelText(/email/i), 'user@test.com');
    await userEvent.click(screen.getByRole('button', { name: /resend/i }));

    await waitFor(() => expect(screen.getByRole('alert')).toBeInTheDocument());
    expect(screen.getByRole('alert')).toHaveClass('MuiAlert-outlinedInfo');
  });

  it('pre-fills the email field from a ?email= query param (arriving here from LoginPage)', async () => {
    renderResend('?email=blocked%40test.com');

    expect(screen.getByLabelText(/email/i)).toHaveValue('blocked@test.com');
  });

  it('explains why the user landed here instead of just saying "Enter your email"', () => {
    // frizat: this page gave zero context — a user redirected here from a failed login had
    // no idea why, which read as part of the "gross, unexplained" flow the user flagged.
    renderResend();

    expect(screen.getByText(/your account isn't confirmed yet/i)).toBeInTheDocument();
  });
});
