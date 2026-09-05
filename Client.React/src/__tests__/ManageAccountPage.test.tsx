import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import ManageAccountPage from '../pages/account/ManageAccountPage';
import { vi } from 'vitest';
import { MemoryRouter } from 'react-router-dom';

const mockNavigate = vi.fn();
vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom');
  return { ...actual, useNavigate: () => mockNavigate };
});

const authState = {
  user: { userId: '123', name: 'testuser', claims: [] },
};
const sessionState = {
  availableLeagues: [] as { leagueId: number; leagueName: string }[],
};

vi.mock('../services/auth', () => ({ useAuth: () => authState }));
vi.mock('../services/session', () => ({ useSession: () => sessionState }));

const renderPage = () =>
  render(
    <MemoryRouter>
      <ManageAccountPage />
    </MemoryRouter>
  );

describe('ManageAccountPage', () => {
  beforeEach(() => {
    mockNavigate.mockReset();
  });

  it('greets the user by name', () => {
    renderPage();
    expect(screen.getByText(/welcome, testuser/i)).toBeInTheDocument();
  });

  it('shows Change Password and Change Username buttons', () => {
    renderPage();
    expect(screen.getByRole('button', { name: /change password/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /change username/i })).toBeInTheDocument();
  });

  it('navigates to the change-username route when Change Username is clicked', async () => {
    renderPage();
    await userEvent.click(screen.getByRole('button', { name: /change username/i }));
    expect(mockNavigate).toHaveBeenCalledWith('/account/manage/changeusername');
  });

  it('navigates to the change-password route when Change Password is clicked', async () => {
    renderPage();
    await userEvent.click(screen.getByRole('button', { name: /change password/i }));
    expect(mockNavigate).toHaveBeenCalledWith('/account/manage/changepassword');
  });
});
