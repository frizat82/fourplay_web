import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi } from 'vitest';

vi.mock('../api/auth', () => ({ requestConfirmEmail: vi.fn() }));

import ResendEmailConfirmationPage from '../pages/account/ResendEmailConfirmationPage';
import { requestConfirmEmail } from '../api/auth';

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
});
