import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { vi } from 'vitest';

const navigateMock = vi.fn();
vi.mock('react-router-dom', async (importOriginal) => {
  const actual = await importOriginal<typeof import('react-router-dom')>();
  return { ...actual, useNavigate: () => navigateMock };
});

import InvalidPasswordResetPage from '../pages/account/InvalidPasswordResetPage';

// frizat: this route renders outside AppLayout with no header/nav — previously this page had
// zero navigation of any kind, a genuine dead end for anyone whose reset link expired.
describe('InvalidPasswordResetPage', () => {
  beforeEach(() => navigateMock.mockReset());

  function renderPage() {
    return render(
      <MemoryRouter>
        <InvalidPasswordResetPage />
      </MemoryRouter>,
    );
  }

  it('offers to request a new reset link', async () => {
    renderPage();

    await userEvent.click(screen.getByRole('button', { name: /request a new reset link/i }));

    expect(navigateMock).toHaveBeenCalledWith('/account/forgotpassword');
  });

  it('has a way back to login', async () => {
    renderPage();

    await userEvent.click(screen.getByRole('button', { name: /back to login/i }));

    expect(navigateMock).toHaveBeenCalledWith('/account/login');
  });
});
