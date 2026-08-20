import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { vi } from 'vitest';

const navigateMock = vi.fn();
vi.mock('react-router-dom', async (importOriginal) => {
  const actual = await importOriginal<typeof import('react-router-dom')>();
  return { ...actual, useNavigate: () => navigateMock };
});

const { pushMock, loginMock } = vi.hoisted(() => ({ pushMock: vi.fn(), loginMock: vi.fn() }));
vi.mock('../services/auth', () => ({ useAuth: () => ({ login: loginMock }) }));
vi.mock('../services/toast', () => ({ useToast: () => ({ push: pushMock }) }));

import LoginPage from '../pages/account/LoginPage';

function renderLogin(search = '') {
  return render(
    <MemoryRouter initialEntries={[`/account/login${search}`]}>
      <LoginPage />
    </MemoryRouter>,
  );
}

async function submitLoginForm() {
  await userEvent.type(screen.getByLabelText(/email/i), 'user@test.com');
  await userEvent.type(screen.getByLabelText(/password/i), 'Passw0rd!');
  await userEvent.click(screen.getByRole('button', { name: /^login$/i }));
}

describe('LoginPage', () => {
  beforeEach(() => {
    navigateMock.mockReset();
    pushMock.mockReset();
    loginMock.mockReset();
  });

  it('navigates to returnUrl on success', async () => {
    loginMock.mockResolvedValue({ succeeded: true, isLockedOut: false, requiresTwoFactor: false, isNotAllowed: false, accessFailedCount: 0 });
    renderLogin();

    await submitLoginForm();

    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith('/dashboard', { replace: true }));
  });

  it('ignores a crafted returnUrl=/logout — /logout is public and would immediately sign the user back out', async () => {
    loginMock.mockResolvedValue({ succeeded: true, isLockedOut: false, requiresTwoFactor: false, isNotAllowed: false, accessFailedCount: 0 });
    renderLogin('?returnUrl=%2Flogout');

    await submitLoginForm();

    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith('/dashboard', { replace: true }));
  });

  it('shows the generic message for ordinary invalid credentials — does not redirect', async () => {
    loginMock.mockResolvedValue({
      succeeded: false, isLockedOut: false, requiresTwoFactor: false, isNotAllowed: false,
      accessFailedCount: 1, message: 'Invalid credentials',
    });
    renderLogin();

    await submitLoginForm();

    await waitFor(() => expect(pushMock).toHaveBeenCalledWith('Invalid credentials', 'error'));
    expect(navigateMock).not.toHaveBeenCalled();
  });

  it('on unconfirmed-email (isNotAllowed), shows an actionable message and redirects to resend with the email prefilled', async () => {
    // Two separate live testers hit exactly this: register -> emails send fine -> try to log
    // in immediately -> generic "User is not allowed to sign in" toast with no indication this
    // just means "click your confirmation email" and no path to fix it from here.
    loginMock.mockResolvedValue({
      succeeded: false, isLockedOut: false, requiresTwoFactor: false, isNotAllowed: true,
      accessFailedCount: 0, message: 'User is not allowed to sign in',
    });
    renderLogin();

    await submitLoginForm();

    await waitFor(() =>
      expect(navigateMock).toHaveBeenCalledWith('/account/resendemailconfirmation?email=user%40test.com'),
    );
    expect(pushMock).toHaveBeenCalledWith(
      expect.stringMatching(/confirm your email/i),
      'warning',
    );
  });
});
