import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { getLeagueUserMappingsForUser } from '../api/league';
import type { LeagueUserMappingDto } from '../types/league';
import { useAuth } from './auth';

interface SessionContextValue {
  availableLeagues: LeagueUserMappingDto[];
  currentLeague: number | null;
  selectLeague: (leagueId: number) => void;
  reloadLeagues: () => Promise<void>;
  clearSession: () => void;
}

const SessionContext = createContext<SessionContextValue | undefined>(undefined);
const LEAGUE_KEY = 'FourPlayWebApp.LeagueId';

const loadStoredLeague = () => {
  const raw = localStorage.getItem(LEAGUE_KEY);
  const parsed = raw ? Number(raw) : null;
  return Number.isFinite(parsed) && parsed !== 0 ? parsed : null;
};

export function SessionProvider({ children }: { children: React.ReactNode }) {
  const { user } = useAuth();
  const [availableLeagues, setAvailableLeagues] = useState<LeagueUserMappingDto[]>([]);
  const [currentLeague, setCurrentLeague] = useState<number | null>(() => loadStoredLeague());

  const persistLeague = useCallback((leagueId: number | null) => {
    if (leagueId === null) {
      localStorage.removeItem(LEAGUE_KEY);
    } else {
      localStorage.setItem(LEAGUE_KEY, String(leagueId));
    }
  }, []);

  const reloadLeagues = useCallback(async () => {
    if (!user?.userId) {
      setAvailableLeagues([]);
      setCurrentLeague(null);
      persistLeague(null);
      return;
    }

    const leagues = (await getLeagueUserMappingsForUser(user.userId)) ?? [];
    setAvailableLeagues(leagues);

    const stored = loadStoredLeague();
    const storedValid = stored !== null && leagues.some((l) => l.leagueId === stored);

    if (storedValid) {
      setCurrentLeague(stored!);
    } else if (leagues.length > 0) {
      setCurrentLeague(leagues[0].leagueId);
      persistLeague(leagues[0].leagueId);
    } else {
      setCurrentLeague(null);
      persistLeague(null);
    }
  }, [persistLeague, user]);

  const selectLeague = useCallback(
    (leagueId: number) => {
      setCurrentLeague(leagueId);
      persistLeague(leagueId);
    },
    [persistLeague]
  );

  const clearSession = useCallback(() => {
    setAvailableLeagues([]);
    setCurrentLeague(null);
    persistLeague(null);
  }, [persistLeague]);

  useEffect(() => {
    void reloadLeagues();
  }, [reloadLeagues]);

  const value = useMemo(
    () => ({ availableLeagues, currentLeague, selectLeague, reloadLeagues, clearSession }),
    [availableLeagues, clearSession, currentLeague, reloadLeagues, selectLeague]
  );

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>;
}

export function useSession() {
  const ctx = useContext(SessionContext);
  if (!ctx) {
    throw new Error('useSession must be used within SessionProvider');
  }
  return ctx;
}
