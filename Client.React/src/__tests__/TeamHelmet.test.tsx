import { render, screen, fireEvent } from '@testing-library/react';
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
    const { container } = render(<TeamHelmet abbr="ill" showLabel={false} />);
    const img = screen.getByRole('img', { name: 'ill' });

    fireEvent.error(img);

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
