import { createContext, useContext, useMemo } from 'react';

export type SportType = 'NFL' | 'CFB';

interface SportsContextValue {
  sport: SportType;
  isCfb: boolean;
  isNfl: boolean;
}

const SportsContext = createContext<SportsContextValue | null>(null);

function detectSport(): SportType {
  return window.location.hostname.startsWith('cfb.') ? 'CFB' : 'NFL';
}

export function SportsProvider({ children }: { children: React.ReactNode }) {
  const sport = detectSport();

  const value = useMemo<SportsContextValue>(
    () => ({ sport, isCfb: sport === 'CFB', isNfl: sport === 'NFL' }),
    [sport]
  );

  return <SportsContext.Provider value={value}>{children}</SportsContext.Provider>;
}

export function useSportContext(): SportsContextValue {
  const ctx = useContext(SportsContext);
  if (!ctx) throw new Error('useSportContext must be used within a SportsProvider');
  return ctx;
}
