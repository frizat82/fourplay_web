import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { vi } from 'vitest';

const navigateMock = vi.fn();
vi.mock('react-router-dom', async (importOriginal) => {
  const actual = await importOriginal<typeof import('react-router-dom')>();
  return { ...actual, useNavigate: () => navigateMock };
});

vi.mock('../api/auth', () => ({ createUser: vi.fn() }));
vi.mock('../api/invitations', () => ({ validateInvitation: vi.fn().mockResolvedValue(null) }));
vi.mock('../services/toast', () => ({ useToast: () => ({ push: vi.fn() }) }));

import RegisterPage from '../pages/account/RegisterPage';
import { createUser } from '../api/auth';

const VALID_PASSWORD = 'Passw0rd!';

function renderWithSearch(search: string) {
  return render(
    <MemoryRouter initialEntries={[`/account/register${search}`]}>
      <RegisterPage />
    </MemoryRouter>,
  );
}

describe('RegisterPage — invite link flow', () => {
  beforeEach(() => {
    navigateMock.mockReset();
    vi.mocked(createUser).mockResolvedValue({ isSuccess: true, userId: 'new-user-1', errors: [] });
  });

  it('hides Invitation Code field when inviteLinkToken is in the URL', () => {
    renderWithSearch('?inviteLinkToken=abc123&returnUrl=/join/abc123');
    expect(screen.queryByLabelText(/invitation code/i)).not.toBeInTheDocument();
  });

  it('shows Invitation Code field when no inviteLinkToken', () => {
    renderWithSearch('');
    expect(screen.getByLabelText(/invitation code/i)).toBeInTheDocument();
  });

  it('passes inviteLinkToken to createUser when registering via link', async () => {
    renderWithSearch('?inviteLinkToken=abc123&returnUrl=/join/abc123');

    await userEvent.type(screen.getByLabelText(/username/i), 'newuser');
    await userEvent.type(screen.getByLabelText(/^email$/i), 'new@test.com');
    await userEvent.type(screen.getByLabelText(/^password$/i), VALID_PASSWORD);
    await userEvent.type(screen.getByLabelText(/confirm password/i), VALID_PASSWORD);
    await userEvent.click(screen.getByRole('button', { name: /register/i }));

    await waitFor(() =>
      expect(createUser).toHaveBeenCalledWith(
        expect.objectContaining({ inviteLinkToken: 'abc123' }),
      ),
    );
  });

  it('does NOT pass inviteLinkToken when using invitation code path', async () => {
    renderWithSearch('?inviteCode=MYCODE');

    await userEvent.type(screen.getByLabelText(/invitation code/i), 'MYCODE');
    await userEvent.type(screen.getByLabelText(/username/i), 'newuser');
    await userEvent.type(screen.getByLabelText(/^email$/i), 'new@test.com');
    await userEvent.type(screen.getByLabelText(/^password$/i), VALID_PASSWORD);
    await userEvent.type(screen.getByLabelText(/confirm password/i), VALID_PASSWORD);
    await userEvent.click(screen.getByRole('button', { name: /register/i }));

    await waitFor(() => expect(createUser).toHaveBeenCalled());
    const callArg = vi.mocked(createUser).mock.calls[0][0];
    expect(callArg.inviteLinkToken).toBeUndefined();
  });
});
