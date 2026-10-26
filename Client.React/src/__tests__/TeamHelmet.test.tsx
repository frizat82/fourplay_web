import { act, render, screen, fireEvent } from '@testing-library/react';
import TeamHelmet from '../components/sports/TeamHelmet';

describe('TeamHelmet', () => {
  it('uses the flat-color SVG shield as the primary source', () => {
    render(<TeamHelmet abbr="ne" />);
    expect(screen.getByRole('img', { name: 'ne' })).toHaveAttribute('src', '/Icons/Helmets/ne.svg');
  });

  // frizat: teams missing an SVG (e.g. Illinois) used to fall back to the neon PNG set, which
  // silently brought the disliked neon look back for exactly those teams. Falls back to a plain
  // text badge instead — no image asset can ever render the neon look again.
  it('falls back to a text badge, not the neon PNG, when the SVG 404s', () => {
    vi.useFakeTimers();
    const { container } = render(<TeamHelmet abbr="ill" showLabel={false} />);

    // A load error is retried before giving up (see TeamArtImage) — fail it through every retry.
    for (let i = 0; i < 3; i++) {
      fireEvent.error(screen.getByRole('img', { name: 'ill' }));
      act(() => { vi.advanceTimersByTime(5000); });
    }
    vi.useRealTimers();

    expect(container.querySelector('img')).not.toBeInTheDocument();
    expect(screen.getByText('ILL')).toBeInTheDocument();
  });

  it('shows the abbreviation label below the badge when showLabel is true', () => {
    render(<TeamHelmet abbr="kc" showLabel />);
    expect(screen.getByText('KC')).toBeInTheDocument();
  });

  it('hides the abbreviation label when showLabel is false', () => {
    render(<TeamHelmet abbr="kc" showLabel={false} />);
    expect(screen.queryByText('KC')).not.toBeInTheDocument();
  });
});
