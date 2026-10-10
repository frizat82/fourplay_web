import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { AxiosError } from 'axios';
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

  // A request that never reached the server (weak signal, timeout) says nothing about whether the
  // user is signed in, so it must not bounce them to the login page — offer a retry instead.
  describe('when the server cannot be reached', () => {
    const networkError = () => new AxiosError('timeout of 15000ms exceeded', AxiosError.ECONNABORTED);

    it('shows a retry prompt instead of redirecting to login', async () => {
      mockedHttp.get.mockRejectedValueOnce(networkError());

      renderProtected();

      await screen.findByText(/can't reach iv league/i);
      expect(screen.queryByText('Login')).not.toBeInTheDocument();
    });

    it('still redirects to login when the server answers 401', async () => {
      mockedHttp.get.mockRejectedValueOnce(
        new AxiosError('Unauthorized', 'ERR_BAD_REQUEST', undefined, undefined, {
          status: 401, statusText: 'Unauthorized', data: null, headers: {}, config: {} as never,
        }),
      );

      renderProtected();

      await screen.findByText('Login');
    });

    it('loads the page when Retry succeeds', async () => {
      mockedHttp.get
        .mockRejectedValueOnce(networkError())
        .mockResolvedValueOnce({ data: { userId: '123', name: 'Test User', claims: [] } });

      renderProtected();
      fireEvent.click(await screen.findByRole('button', { name: /retry/i }));

      await screen.findByText('Protected');
    });

    it('retries on its own when the device comes back online', async () => {
      mockedHttp.get
        .mockRejectedValueOnce(networkError())
        .mockResolvedValue({ data: { userId: '123', name: 'Test User', claims: [] } });

      renderProtected();
      await screen.findByText(/can't reach iv league/i);
      // The listener attaches in an effect just after the retry prompt paints, so a single event
      // fired the instant the prompt appears can land before it exists — keep signalling instead.
      await waitFor(() => {
        act(() => { window.dispatchEvent(new Event('online')); });
        expect(screen.getByText('Protected')).toBeInTheDocument();
      });
    });
  });
});
