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

  // frizat: the dark-mode neon PNG set was reverted — SVG is primary in both modes now.
  it('uses the flat-color SVG shield as the primary source in dark mode too', () => {
    themeState.mode = 'dark';
    render(<TeamHelmet abbr="ne" />);
    expect(screen.getByRole('img', { name: 'ne' })).toHaveAttribute('src', '/Icons/Helmets/ne.svg');
  });

  it('falls back to the PNG once, then hides, when the SVG 404s', () => {
    render(<TeamHelmet abbr="ne" />);
    const img = screen.getByRole('img', { name: 'ne' });

    fireEvent.error(img);
    expect(img).toHaveAttribute('src', '/Icons/Helmets/ne.png');
    expect(img.style.visibility).not.toBe('hidden');

    fireEvent.error(img);
    expect(img.style.visibility).toBe('hidden');
  });

  it('falls back to the PNG once, then hides, when the SVG 404s in dark mode too', () => {
    themeState.mode = 'dark';
    render(<TeamHelmet abbr="ne" />);
    const img = screen.getByRole('img', { name: 'ne' });

    fireEvent.error(img);
    expect(img).toHaveAttribute('src', '/Icons/Helmets/ne.png');

    fireEvent.error(img);
    expect(img.style.visibility).toBe('hidden');
  });
});
