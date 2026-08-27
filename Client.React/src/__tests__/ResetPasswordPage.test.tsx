import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { vi } from 'vitest';

const navigateMock = vi.fn();
vi.mock('react-router-dom', async (importOriginal) => {
  const actual = await importOriginal<typeof import('react-router-dom')>();
  return { ...actual, useNavigate: () => navigateMock };
});

vi.mock('../api/auth', () => ({ resetPassword: vi.fn() }));
vi.mock('../services/toast', () => ({ useToast: () => ({ push: vi.fn() }) }));
// Bypass real base64 validation — this test only cares about the cancel link, not the code param.
vi.mock('../utils/base64', () => ({ isValidBase64Url: () => true, decodeBase64Url: () => 'token' }));

import ResetPasswordPage from '../pages/account/ResetPasswordPage';
import { resetPassword } from '../api/auth';

// frizat: previously had no way to cancel/return to login before submitting.
describe('ResetPasswordPage', () => {
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
});
