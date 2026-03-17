import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { http } from '../api/http';
import type { LoginRequest, SignInResultDto, UserInfo } from '../types/auth';
import { isAdmin } from '../utils/auth';

interface AuthContextValue {
  user: UserInfo | null;
  loading: boolean;
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

  const refresh = useCallback(async () => {
    try {
      const response = await http.get<UserInfo>('/api/auth/me');
      setUser(isValidUserInfo(response.data) ? response.data : null);
    } catch {
      setUser(null);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void refresh();
  }, [refresh]);

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
    () => ({ user, loading, login, logout, refresh }),
    [loading, login, logout, refresh, user]
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

export function RequireAuth({ children }: { children: React.ReactNode }) {
  const { user, loading } = useAuth();
  const location = useLocation();

  if (loading) {
    return <div>Loading...</div>;
  }

  if (!user) {
    const returnUrl = encodeURIComponent(location.pathname + location.search);
    return <Navigate to={`/account/login?returnUrl=${returnUrl}`} replace />;
  }

  return <>{children}</>;
}

export function RequireAdmin({ children }: { children: React.ReactNode }) {
  const { user, loading } = useAuth();
  const location = useLocation();

  if (loading) {
    return <div>Loading...</div>;
  }

  if (!user) {
    const returnUrl = encodeURIComponent(location.pathname + location.search);
    return <Navigate to={`/account/login?returnUrl=${returnUrl}`} replace />;
  }

  if (!isAdmin(user)) {
    return <Navigate to="/dashboard" replace />;
  }

  return <>{children}</>;
}
