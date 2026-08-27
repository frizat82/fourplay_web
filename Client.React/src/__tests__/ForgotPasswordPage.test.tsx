import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { vi } from 'vitest';

const navigateMock = vi.fn();
vi.mock('react-router-dom', async (importOriginal) => {
  const actual = await importOriginal<typeof import('react-router-dom')>();
  return { ...actual, useNavigate: () => navigateMock };
});

vi.mock('../api/auth', () => ({ forgotPassword: vi.fn() }));
vi.mock('../services/toast', () => ({ useToast: () => ({ push: vi.fn() }) }));

import ForgotPasswordPage from '../pages/account/ForgotPasswordPage';
import { forgotPassword } from '../api/auth';

// frizat: previously had no way to cancel/return to login before submitting.
describe('ForgotPasswordPage', () => {
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
});
