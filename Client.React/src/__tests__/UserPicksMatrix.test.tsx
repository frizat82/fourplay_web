import { render } from '@testing-library/react';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import UserPicksMatrix from '../components/UserPicksMatrix';
import type { NflPickDto } from '../types/picks';

vi.mock('../components/sports/TeamHelmet', () => ({
  default: ({ abbr }: { abbr: string }) => <div data-testid={`helmet-${abbr}`}>{abbr}</div>,
}));

const picks: NflPickDto[] = [
  { id: 1, userId: 'u1', userName: 'alice', team: 'KC', pick: 'Spread', season: 2025, nflWeek: 1, leagueId: 1, dateCreated: '2025-09-01T00:00:00Z' },
];

function renderMatrix(mode: 'light' | 'dark') {
  return render(
    <ThemeProvider theme={createTheme({ palette: { mode } })}>
      <UserPicksMatrix users={['alice']} picks={picks} spreads={{}} requiredPicks={1} />
    </ThemeProvider>,
  );
}

describe('UserPicksMatrix — dark mode cell coloring', () => {
  // frizat: TeamHelmet's dark-mode logos are neon assets meant to glow against a dark
  // background (see TeamHelmet.tsx); the matrix cell used a hardcoded light gray in every mode,
  // washing the helmet out. The cell background must actually change with the theme.
  it('uses a dark neutral cell background in dark mode, not the light-mode gray', () => {
    const { getByTestId } = renderMatrix('dark');
    const cell = getByTestId('helmet-KC').parentElement;
    expect(cell).toHaveStyle({ backgroundColor: 'rgb(66, 66, 66)' }); // MUI grey.800
  });

  it('keeps the light neutral cell background in light mode', () => {
    const { getByTestId } = renderMatrix('light');
    const cell = getByTestId('helmet-KC').parentElement;
    expect(cell).toHaveStyle({ backgroundColor: 'rgb(238, 238, 238)' }); // MUI grey.200
  });
});
