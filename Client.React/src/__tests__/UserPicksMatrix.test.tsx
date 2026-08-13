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

describe('UserPicksMatrix — spread vs Over/Under picks', () => {
  // frizat: a user can have both a spread pick and a separate Over/Under pick in the same
  // postseason week (O/U isn't counted toward requiredPicks). Indexing into a single mixed-type
  // array by position meant whichever pick sorted first won the one available column and the
  // other was silently dropped — so a user's O/U pick could vanish entirely, or overwrite their
  // spread pick's cell.
  const mixedPicks: NflPickDto[] = [
    { id: 1, userId: 'u1', userName: 'bob', team: 'NE', pick: 'Spread', season: 2025, nflWeek: 22, leagueId: 1, dateCreated: '' },
    { id: 2, userId: 'u1', userName: 'bob', team: 'NE', pick: 'Over', season: 2025, nflWeek: 22, leagueId: 1, dateCreated: '' },
  ];

  function renderMixed() {
    return render(
      <ThemeProvider theme={createTheme()}>
        <UserPicksMatrix users={['bob']} picks={mixedPicks} spreads={{}} requiredPicks={1} />
      </ThemeProvider>,
    );
  }

  it('shows both the spread pick and the Over/Under pick, in separate columns', () => {
    renderMixed();
    expect(screen.getByText('O/U')).toBeInTheDocument();
    expect(screen.getAllByTestId('helmet-NE')).toHaveLength(2);
  });

  it('does not add an O/U column when no one has an Over/Under pick this week', () => {
    render(
      <ThemeProvider theme={createTheme()}>
        <UserPicksMatrix users={['alice']} picks={picks} spreads={{}} requiredPicks={1} />
      </ThemeProvider>,
    );
    expect(screen.queryByText('O/U')).not.toBeInTheDocument();
  });
});
