import { render, screen, fireEvent } from '@testing-library/react';
import TeamLogo from '../components/sports/TeamLogo';

describe('TeamLogo', () => {
  it('uses the downloaded ESPN logo PNG from the sport-scoped folder as the primary source', () => {
    render(<TeamLogo abbr="ne" sport="nfl" />);
    expect(screen.getByRole('img', { name: 'ne' })).toHaveAttribute('src', '/Icons/Logos/nfl/ne.png');
  });

  // frizat-cwj: a few abbreviations are shared by an NFL team and a CFB team (MIA:
  // Dolphins/Hurricanes) — sport-scoped subfolders, not a flat abbr-keyed one, keep them distinct.
  it('resolves the same abbreviation to a different file per sport', () => {
    render(<TeamLogo abbr="mia" sport="cfb" />);
    expect(screen.getByRole('img', { name: 'mia' })).toHaveAttribute('src', '/Icons/Logos/cfb/mia.png');
  });

  it('falls back to the same text badge TeamHelmet uses when the logo 404s', () => {
    const { container } = render(<TeamLogo abbr="xyz" sport="nfl" showLabel={false} />);
    const img = screen.getByRole('img', { name: 'xyz' });

    fireEvent.error(img);

    expect(container.querySelector('img')).not.toBeInTheDocument();
    expect(screen.getByText('XYZ')).toBeInTheDocument();
  });

  it('shows the abbreviation label below the logo when showLabel is true', () => {
    render(<TeamLogo abbr="kc" sport="nfl" showLabel />);
    expect(screen.getByText('KC')).toBeInTheDocument();
  });

  it('hides the abbreviation label when showLabel is false', () => {
    render(<TeamLogo abbr="kc" sport="nfl" showLabel={false} />);
    expect(screen.queryByText('KC')).not.toBeInTheDocument();
  });
});
