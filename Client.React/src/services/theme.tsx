import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';

type ThemeMode = 'light' | 'dark';
/** Also read by index.html's pre-paint script (themeMode.test.tsx pins the two together). */
export const THEME_STORAGE_KEY = 'FourPlayWebApp.ThemeMode';

interface ThemeContextValue {
  mode: ThemeMode;
  toggleTheme: () => void;
}

const ThemeContext = createContext<ThemeContextValue>({ mode: 'light', toggleTheme: () => {} });

export function ThemeModeProvider({ children }: { children: React.ReactNode }) {
  const [mode, setMode] = useState<ThemeMode>(() => {
    // index.html's inline script already resolved this before first paint — start from its answer.
    const painted = document.documentElement.getAttribute('data-theme');
    if (painted === 'dark' || painted === 'light') return painted;
    const stored = localStorage.getItem(THEME_STORAGE_KEY);
    if (stored === 'dark' || stored === 'light') return stored;
    return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  });

  useEffect(() => {
    localStorage.setItem(THEME_STORAGE_KEY, mode);
    document.documentElement.setAttribute('data-theme', mode);
  }, [mode]);

  const toggleTheme = useCallback(() => setMode((m) => (m === 'light' ? 'dark' : 'light')), []);

  const value = useMemo(() => ({ mode, toggleTheme }), [mode, toggleTheme]);

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}

export function useThemeMode() {
  return useContext(ThemeContext);
}
