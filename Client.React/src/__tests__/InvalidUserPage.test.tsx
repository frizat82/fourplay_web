import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { vi } from 'vitest';

const navigateMock = vi.fn();
vi.mock('react-router-dom', async (importOriginal) => {
  const actual = await importOriginal<typeof import('react-router-dom')>();
  return { ...actual, useNavigate: () => navigateMock };
});

import InvalidUserPage from '../pages/account/InvalidUserPage';

// frizat: this route renders outside AppLayout with no header/nav — previously this page had
// zero navigation of any kind, a genuine dead end.
describe('InvalidUserPage', () => {
  it('has a way back to login', async () => {
    navigateMock.mockReset();
    render(
      <MemoryRouter>
        <InvalidUserPage />
      </MemoryRouter>,
    );

    await userEvent.click(screen.getByRole('button', { name: /go to login/i }));

    expect(navigateMock).toHaveBeenCalledWith('/account/login');
  });
});
