import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { vi } from 'vitest';

const navigateMock = vi.fn();
vi.mock('react-router-dom', async (importOriginal) => {
  const actual = await importOriginal<typeof import('react-router-dom')>();
  return { ...actual, useNavigate: () => navigateMock };
});

import ForgotPasswordConfirmationPage from '../pages/account/ForgotPasswordConfirmationPage';

// frizat: this route renders outside AppLayout with no header/nav, and standalone PWA mode has
// no browser chrome to fall back on — previously this page had zero navigation of any kind.
describe('ForgotPasswordConfirmationPage', () => {
  it('has a way back to login', async () => {
    navigateMock.mockReset();
    render(
      <MemoryRouter>
        <ForgotPasswordConfirmationPage />
      </MemoryRouter>,
    );

    await userEvent.click(screen.getByRole('button', { name: /go to login/i }));

    expect(navigateMock).toHaveBeenCalledWith('/account/login');
  });
});
