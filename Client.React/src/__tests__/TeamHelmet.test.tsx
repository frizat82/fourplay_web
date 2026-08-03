import { render, screen, fireEvent } from '@testing-library/react';
import { vi } from 'vitest';
import TeamHelmet from '../components/sports/TeamHelmet';

const themeState = { mode: 'light' as 'light' | 'dark' };
vi.mock('../services/theme', () => ({ useThemeMode: () => ({ mode: themeState.mode, toggleTheme: vi.fn() }) }));

describe('TeamHelmet', () => {
  beforeEach(() => {
    themeState.mode = 'light';
  });

  it('uses the flat-color SVG shield as the primary source in light mode', () => {
    render(<TeamHelmet abbr="ne" />);
    expect(screen.getByRole('img', { name: 'ne' })).toHaveAttribute('src', '/Icons/Helmets/ne.svg');
  });

  it('uses the neon PNG as the primary source in dark mode', () => {
    themeState.mode = 'dark';
    render(<TeamHelmet abbr="ne" />);
    expect(screen.getByRole('img', { name: 'ne' })).toHaveAttribute('src', '/Icons/Helmets/ne.png');
  });

  it('falls back to the PNG once, then hides, when the light-mode SVG 404s', () => {
    render(<TeamHelmet abbr="ne" />);
    const img = screen.getByRole('img', { name: 'ne' });

    fireEvent.error(img);
    expect(img).toHaveAttribute('src', '/Icons/Helmets/ne.png');
    expect(img.style.visibility).not.toBe('hidden');

    fireEvent.error(img);
    expect(img.style.visibility).toBe('hidden');
  });

  it('falls back to the SVG once, then hides, when the dark-mode PNG 404s', () => {
    themeState.mode = 'dark';
    render(<TeamHelmet abbr="ne" />);
    const img = screen.getByRole('img', { name: 'ne' });

    fireEvent.error(img);
    expect(img).toHaveAttribute('src', '/Icons/Helmets/ne.svg');

    fireEvent.error(img);
    expect(img.style.visibility).toBe('hidden');
  });
});
