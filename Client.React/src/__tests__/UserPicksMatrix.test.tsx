import { render, screen } from '@testing-library/react';
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

describe('UserPicksMatrix — Over/Under is an alternate pick type, not an additional pick', () => {
  // frizat: AddPicks' server-side validation caps total picks (any type) at requiredPicks — a
  // real user's Over/Under pick REPLACES one of their spread picks, it never adds a pick beyond
  // requiredPicks. One badge per required-pick column; column count always equals requiredPicks.
  const mixedPicks: NflPickDto[] = [
    { id: 1, userId: 'u1', userName: 'bob', team: 'NE', pick: 'Over', season: 2025, nflWeek: 19, leagueId: 1, dateCreated: '' },
    { id: 2, userId: 'u1', userName: 'bob', team: 'KC', pick: 'Spread', season: 2025, nflWeek: 19, leagueId: 1, dateCreated: '' },
    { id: 3, userId: 'u1', userName: 'bob', team: 'BUF', pick: 'Spread', season: 2025, nflWeek: 19, leagueId: 1, dateCreated: '' },
  ];

  it('renders exactly one badge per required-pick column, regardless of pick type', () => {
    render(
      <ThemeProvider theme={createTheme()}>
        <UserPicksMatrix users={['bob']} picks={mixedPicks} spreads={{}} requiredPicks={3} />
      </ThemeProvider>,
    );
    expect(screen.getAllByRole('columnheader')).toHaveLength(4); // User + Pick 1-3
    expect(screen.getAllByTestId('helmet-NE')).toHaveLength(1);
    expect(screen.getAllByTestId('helmet-KC')).toHaveLength(1);
    expect(screen.getAllByTestId('helmet-BUF')).toHaveLength(1);
  });

  it('column count always equals requiredPicks', () => {
    render(
      <ThemeProvider theme={createTheme()}>
        <UserPicksMatrix users={['bob']} picks={mixedPicks} spreads={{}} requiredPicks={4} />
      </ThemeProvider>,
    );
    expect(screen.getAllByRole('columnheader')).toHaveLength(5); // User + Pick 1-4
  });
});
