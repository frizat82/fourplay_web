import { render, screen } from '@testing-library/react';
import TeamArt from '../components/sports/TeamArt';

describe('TeamArt', () => {
  afterEach(() => vi.unstubAllEnvs());

  it('renders the synthetic helmet badge by default (badges mode)', () => {
    render(<TeamArt abbr="ne" sport="nfl" />);
    expect(screen.getByRole('img', { name: 'ne' })).toHaveAttribute('src', '/Icons/Helmets/ne.svg');
  });

  it('renders the real ESPN logo from the sport-scoped folder when VITE_TEAM_ART_MODE=logos', () => {
    vi.stubEnv('VITE_TEAM_ART_MODE', 'logos');
    render(<TeamArt abbr="ne" sport="nfl" />);
    expect(screen.getByRole('img', { name: 'ne' })).toHaveAttribute('src', '/Icons/Logos/nfl/ne.png');
  });

  it('uses the cfb logo folder for sport="cfb"', () => {
    vi.stubEnv('VITE_TEAM_ART_MODE', 'logos');
    render(<TeamArt abbr="mia" sport="cfb" />);
    expect(screen.getByRole('img', { name: 'mia' })).toHaveAttribute('src', '/Icons/Logos/cfb/mia.png');
  });
});
