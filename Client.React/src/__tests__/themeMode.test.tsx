import { render, screen } from '@testing-library/react';
import indexHtml from '../../index.html?raw';
import { THEME_STORAGE_KEY, ThemeModeProvider, useThemeMode } from '../services/theme';

function ShowMode() {
  return <span>{useThemeMode().mode}</span>;
}

describe('ThemeModeProvider', () => {
  afterEach(() => {
    document.documentElement.removeAttribute('data-theme');
    localStorage.clear();
  });

  // index.html's inline script resolves the theme before first paint (no light flash on a dark cold
  // load); the provider must start from what it decided, not re-derive a possibly different answer.
  it('starts from the data-theme attribute the pre-paint script set', () => {
    document.documentElement.setAttribute('data-theme', 'dark');
    localStorage.setItem(THEME_STORAGE_KEY, 'light');
    render(<ThemeModeProvider><ShowMode /></ThemeModeProvider>);
    expect(screen.getByText('dark')).toBeInTheDocument();
  });

  it('falls back to the stored mode when no attribute was set', () => {
    localStorage.setItem(THEME_STORAGE_KEY, 'dark');
    render(<ThemeModeProvider><ShowMode /></ThemeModeProvider>);
    expect(screen.getByText('dark')).toBeInTheDocument();
  });

  // The page background has broken 4 times (CLAUDE.md); a renamed key on one side only would
  // silently make every cold load ignore the saved choice.
  it('reads the same storage key as the pre-paint script in index.html', () => {
    expect(indexHtml).toContain(`localStorage.getItem('${THEME_STORAGE_KEY}')`);
  });
});
