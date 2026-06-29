import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { getLeagueUserMappingsForUser, getMyLeagues } from '../api/league';
import type { LeagueUserMappingDto } from '../types/league';
import type { LeagueInfoDto } from '../types/admin';
import { useAuth } from './auth';
import { useSportContext } from './sport';

interface SessionContextValue {
  availableLeagues: LeagueUserMappingDto[];
  currentLeague: number | null;
  selectLeague: (leagueId: number) => void;
  reloadLeagues: () => Promise<void>;
  clearSession: () => void;
  hasNflAccess: boolean;
  hasCfbAccess: boolean;
  leaguesLoaded: boolean;
  isLeagueOwner: boolean;
  ownedLeagues: LeagueInfoDto[];
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
  const { sport } = useSportContext();
  const [availableLeagues, setAvailableLeagues] = useState<LeagueUserMappingDto[]>([]);
  const [currentLeague, setCurrentLeague] = useState<number | null>(() => loadStoredLeague());
  const [hasNflAccess, setHasNflAccess] = useState(false);
  const [hasCfbAccess, setHasCfbAccess] = useState(false);
  const [leaguesLoaded, setLeaguesLoaded] = useState(false);
  const [ownedLeagues, setOwnedLeagues] = useState<LeagueInfoDto[]>([]);

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

    const [allLeagues, myLeagues] = await Promise.all([
      getLeagueUserMappingsForUser(user.userId).then((d) => d ?? []),
      getMyLeagues().catch(() => [] as LeagueInfoDto[]),
    ]);
    setHasNflAccess(allLeagues.some((l) => l.leagueType === 0));
    setHasCfbAccess(allLeagues.some((l) => l.leagueType === 1));
    // Filter leagues to match the current sport context (leagueType: 0=NFL, 1=CFB)
    const leagues = allLeagues.filter((l) =>
      sport === 'CFB' ? l.leagueType === 1 : l.leagueType === 0
    );
    setAvailableLeagues(leagues);
    const sportType = sport === 'CFB' ? 'Cfb' : 'Nfl';
    setOwnedLeagues(myLeagues.filter((l) => l.leagueType === sportType));

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
    setLeaguesLoaded(true);
  }, [persistLeague, user, sport]);

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
    void reloadLeagues().catch((err: unknown) => {
      console.error('Failed to load leagues', err);
    });
  }, [reloadLeagues]);

  const isLeagueOwner = ownedLeagues.length > 0;

  const value = useMemo(
    () => ({ availableLeagues, currentLeague, selectLeague, reloadLeagues, clearSession, hasNflAccess, hasCfbAccess, leaguesLoaded, isLeagueOwner, ownedLeagues }),
    [availableLeagues, clearSession, currentLeague, reloadLeagues, selectLeague, hasNflAccess, hasCfbAccess, leaguesLoaded, isLeagueOwner, ownedLeagues]
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
