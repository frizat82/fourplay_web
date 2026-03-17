import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { vi } from 'vitest';
import { AuthProvider, RequireAuth } from '../services/auth';

vi.mock('../api/http', () => ({
  http: {
    get: vi.fn(),
    post: vi.fn(),
  },
}));

import { http } from '../api/http';

const mockedHttp = http as unknown as {
  get: ReturnType<typeof vi.fn>;
  post: ReturnType<typeof vi.fn>;
};

function renderProtected(initialPath = '/protected') {
  render(
    <MemoryRouter initialEntries={[initialPath]}>
      <AuthProvider>
        <Routes>
          <Route
            path="/protected"
            element={
              <RequireAuth>
                <div>Protected</div>
              </RequireAuth>
            }
          />
          <Route path="/account/login" element={<div>Login</div>} />
        </Routes>
      </AuthProvider>
    </MemoryRouter>
  );
}

describe('AuthProvider/RequireAuth', () => {
  beforeEach(() => {
    mockedHttp.get.mockReset();
    mockedHttp.post.mockReset();
  });

  it('renders protected content when /me succeeds', async () => {
    mockedHttp.get.mockResolvedValueOnce({
      data: { userId: '123', name: 'Test User', claims: [] },
    });

    renderProtected();

    await screen.findByText('Protected');
  });

  it('redirects to login when /me fails', async () => {
    mockedHttp.get.mockRejectedValueOnce(new Error('Unauthorized'));

    renderProtected();

    await screen.findByText('Login');
  });
});
