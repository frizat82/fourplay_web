import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { http } from '../api/http';
import RetryPanel from '../components/RetryPanel';
import RouteFallback from '../components/RouteFallback';
import { isNetworkError } from '../utils/apiError';
import type { LoginRequest, SignInResultDto, UserInfo } from '../types/auth';
import { isAdmin } from '../utils/auth';
import { buildLoginUrl } from '../utils/url';

interface AuthContextValue {
  user: UserInfo | null;
  loading: boolean;
  /** The session check never reached the server (weak signal, timeout) — signed-in state unknown. */
  unreachable: boolean;
  login: (payload: LoginRequest) => Promise<SignInResultDto>;
  logout: () => Promise<void>;
  refresh: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

function isValidUserInfo(payload: unknown): payload is UserInfo {
  if (!payload || typeof payload !== 'object') return false;
  const candidate = payload as Partial<UserInfo>;
  return (
    typeof candidate.userId === 'string' &&
    candidate.userId.length > 0 &&
    typeof candidate.name === 'string' &&
    Array.isArray(candidate.claims)
  );
}

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<UserInfo | null>(null);
  const [loading, setLoading] = useState(true);
  const [unreachable, setUnreachable] = useState(false);

  const refresh = useCallback(async () => {
    try {
      const response = await http.get<UserInfo>('/api/auth/me');
      setUser(isValidUserInfo(response.data) ? response.data : null);
      setUnreachable(false);
    } catch (error) {
      // No response at all says nothing about whether the user is signed in, so don't treat it
      // as signed out (that bounced people on a weak signal to the login page). Keep any user we
      // already had and let the caller offer a retry.
      const noResponse = isNetworkError(error);
      if (!noResponse) setUser(null);
      setUnreachable(noResponse);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  // Retry by itself the moment the phone gets its connection back.
  useEffect(() => {
    if (!unreachable) return;
    const onOnline = () => void refresh();
    window.addEventListener('online', onOnline);
    return () => window.removeEventListener('online', onOnline);
  }, [unreachable, refresh]);

  const login = useCallback(
    async (payload: LoginRequest) => {
      try {
        const response = await http.post<SignInResultDto>('/api/auth/login', payload);
        if (response.data.succeeded) {
          await refresh();
        }
        return response.data;
      } catch {
        return { succeeded: false, isLockedOut: false, requiresTwoFactor: false, isNotAllowed: false, accessFailedCount: 0, message: 'Invalid credentials' } satisfies SignInResultDto;
      }
    },
    [refresh]
  );

  const logout = useCallback(async () => {
    try {
      await http.post('/api/auth/logout');
    } catch {
      // Even if server logout fails, clear client auth state to avoid lock-in UX.
    } finally {
      setUser(null);
    }
  }, []);

  const value = useMemo(
    () => ({ user, loading, unreachable, login, logout, refresh }),
    [loading, unreachable, login, logout, refresh, user]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) {
    throw new Error('useAuth must be used within AuthProvider');
  }
  return ctx;
}

/**
 * What to render while the signed-in state isn't known yet: a loading placeholder, or a retry
 * prompt when the session check couldn't reach the server. Null once auth has resolved.
 */
export function useAuthPending(): React.ReactElement | null {
  const { user, loading, unreachable, refresh } = useAuth();
  if (loading) return <RouteFallback />;
  if (unreachable && !user) {
    return <RetryPanel title="Can't reach IV League" message="Check your signal and try again." actionLabel="Retry" onAction={refresh} />;
  }
  return null;
}

export function RequireAuth({ children }: { children: React.ReactNode }) {
  const { user } = useAuth();
  const pending = useAuthPending();
  const location = useLocation();

  if (pending) {
    return pending;
  }

  if (!user) {
    return <Navigate to={buildLoginUrl(location.pathname + location.search)} replace />;
  }

  return <>{children}</>;
}

export function RequireAdmin({ children }: { children: React.ReactNode }) {
  const { user } = useAuth();
  const pending = useAuthPending();
  const location = useLocation();

  if (pending) {
    return pending;
  }

  if (!user) {
    return <Navigate to={buildLoginUrl(location.pathname + location.search)} replace />;
  }

  if (!isAdmin(user)) {
    return <Navigate to="/dashboard" replace />;
  }

  return <>{children}</>;
}
