import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { vi } from 'vitest';

const navigateMock = vi.fn();
vi.mock('react-router-dom', async (importOriginal) => {
  const actual = await importOriginal<typeof import('react-router-dom')>();
  return { ...actual, useNavigate: () => navigateMock };
});

import LockoutPage from '../pages/account/LockoutPage';

// frizat: this route renders outside AppLayout with no header/nav — previously this page had
// zero navigation of any kind, a genuine dead end for a locked-out user waiting it out.
describe('LockoutPage', () => {
  it('has a way back to login', async () => {
    navigateMock.mockReset();
    render(
      <MemoryRouter>
        <LockoutPage />
      </MemoryRouter>,
    );

    await userEvent.click(screen.getByRole('button', { name: /back to login/i }));

    expect(navigateMock).toHaveBeenCalledWith('/account/login');
  });
});
