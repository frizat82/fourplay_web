import { render, screen } from '@testing-library/react';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import UserPicksMatrix from '../components/UserPicksMatrix';
import type { NflPickDto } from '../types/picks';

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
  // frizat: dark-mode neon logo assets are no longer used in the Matrix view at all (see the
  // "text-only badges" describe block below), but the cell background must still actually
  // change with the theme rather than staying a hardcoded light gray.
  it('uses a dark neutral cell background in dark mode, not the light-mode gray', () => {
    const { getByText } = renderMatrix('dark');
    const cell = getByText('KC').parentElement;
    expect(cell).toHaveStyle({ backgroundColor: 'rgb(66, 66, 66)' }); // MUI grey.800
  });

  it('keeps the light neutral cell background in light mode', () => {
    const { getByText } = renderMatrix('light');
    const cell = getByText('KC').parentElement;
    expect(cell).toHaveStyle({ backgroundColor: 'rgb(238, 238, 238)' }); // MUI grey.200
  });
});

describe('UserPicksMatrix — text-only badges (no team logo)', () => {
  // frizat: the helmet logo + 9px abbreviation label was hard to read at a glance in the
  // Matrix view specifically (unlike GameCard, where the logo has room to breathe) — drop the
  // logo here and show only a much larger, bold team abbreviation.
  it('does not render a team helmet/logo image in the Matrix view', () => {
    renderMatrix('light');
    expect(screen.queryByRole('img')).not.toBeInTheDocument();
  });

  it('renders the team abbreviation as large, readable text', () => {
    renderMatrix('light');
    const label = screen.getByText('KC');
    expect(label).toHaveStyle({ fontSize: '22px', fontWeight: '800' });
  });

  it('shrinks the text for 4-character CFB abbreviations so it fits the 60px badge (e.g. UTSA, UNLV, WASH)', () => {
    // frizat: this component is shared, unmodified, by CFB's ScoresPage Matrix view — CFB
    // abbreviations run up to 4 characters, unlike NFL's 2-3, and the fixed 22px size below
    // was only ever validated against NFL codes.
    const cfbPicks: NflPickDto[] = [
      { id: 1, userId: 'u1', userName: 'alice', team: 'UTSA', pick: 'Spread', season: 2025, nflWeek: 1, leagueId: 1, dateCreated: '' },
    ];
    render(
      <ThemeProvider theme={createTheme()}>
        <UserPicksMatrix users={['alice']} picks={cfbPicks} spreads={{}} requiredPicks={1} />
      </ThemeProvider>,
    );
    const label = screen.getByText('UTSA');
    expect(label).toHaveStyle({ fontSize: '16px', fontWeight: '800' });
  });
});

describe('UserPicksMatrix — Over/Under shown as a bold word, not a small icon', () => {
  // frizat: a tiny corner arrow icon competing with the win/loss-colored badge background and
  // the large team text was easy to miss — spell out OVER/UNDER instead, same size treatment
  // as the team abbreviation label.
  it('shows the word OVER for an Over pick', () => {
    const overPick: NflPickDto[] = [
      { id: 1, userId: 'u1', userName: 'alice', team: 'KC', pick: 'Over', season: 2025, nflWeek: 19, leagueId: 1, dateCreated: '' },
    ];
    render(
      <ThemeProvider theme={createTheme()}>
        <UserPicksMatrix users={['alice']} picks={overPick} spreads={{}} requiredPicks={1} />
      </ThemeProvider>,
    );
    expect(screen.getByText('OVER')).toBeInTheDocument();
  });

  it('shows the word UNDER for an Under pick', () => {
    const underPick: NflPickDto[] = [
      { id: 1, userId: 'u1', userName: 'alice', team: 'KC', pick: 'Under', season: 2025, nflWeek: 19, leagueId: 1, dateCreated: '' },
    ];
    render(
      <ThemeProvider theme={createTheme()}>
        <UserPicksMatrix users={['alice']} picks={underPick} spreads={{}} requiredPicks={1} />
      </ThemeProvider>,
    );
    expect(screen.getByText('UNDER')).toBeInTheDocument();
  });

  it('does not show an OVER/UNDER label for a Spread pick', () => {
    renderMatrix('light');
    expect(screen.queryByText('OVER')).not.toBeInTheDocument();
    expect(screen.queryByText('UNDER')).not.toBeInTheDocument();
  });

  it('does not render an arrow icon anymore', () => {
    const overPick: NflPickDto[] = [
      { id: 1, userId: 'u1', userName: 'alice', team: 'KC', pick: 'Over', season: 2025, nflWeek: 19, leagueId: 1, dateCreated: '' },
    ];
    const { container } = render(
      <ThemeProvider theme={createTheme()}>
        <UserPicksMatrix users={['alice']} picks={overPick} spreads={{}} requiredPicks={1} />
      </ThemeProvider>,
    );
    expect(container.querySelector('[data-testid="ArrowCircleUpIcon"]')).not.toBeInTheDocument();
    expect(container.querySelector('[data-testid="ArrowCircleDownIcon"]')).not.toBeInTheDocument();
  });

  // frizat: the badge already encoded win/loss three different ways at once — the tinted
  // background, a colored OVER/UNDER label, and a corner check/cancel icon — which read as
  // cluttered rather than clear (user feedback: "hard to see w/ the check box"). The
  // background alone now carries the win/loss signal; text stays a single neutral ink color,
  // same as the team abbreviation, in every case (win, loss, or no result yet).
  it.each([
    ['light', 'a winning pick', { team: 'KC', isWinner: true, isOverWinner: true, isUnderWinner: false, spread: -3, over: 45, under: 45 }],
    ['light', 'a losing pick', { team: 'KC', isWinner: false, isOverWinner: false, isUnderWinner: true, spread: -3, over: 45, under: 45 }],
    ['light', 'a pick with no result yet', undefined],
    ['dark', 'a winning pick', { team: 'KC', isWinner: true, isOverWinner: true, isUnderWinner: false, spread: -3, over: 45, under: 45 }],
    ['dark', 'a losing pick', { team: 'KC', isWinner: false, isOverWinner: false, isUnderWinner: true, spread: -3, over: 45, under: 45 }],
    ['dark', 'a pick with no result yet', undefined],
  ] as const)('renders the OVER/UNDER label in the same neutral ink as the team name in %s mode for %s', (mode, _label, spread) => {
    // frizat: /code-review caught that this only ever rendered in light mode — a future
    // dark-mode-only color override on just one of the two labels would've silently passed.
    const pick: NflPickDto[] = [
      { id: 1, userId: 'u1', userName: 'alice', team: 'KC', pick: 'Over', season: 2025, nflWeek: 19, leagueId: 1, dateCreated: '' },
    ];
    render(
      <ThemeProvider theme={createTheme({ palette: { mode } })}>
        <UserPicksMatrix users={['alice']} picks={pick} spreads={spread ? { KC: spread } : {}} requiredPicks={1} />
      </ThemeProvider>,
    );
    const teamLabel = screen.getByText('KC');
    const ouLabel = screen.getByText('OVER');
    expect(getComputedStyle(ouLabel).color).toBe(getComputedStyle(teamLabel).color);
  });

  it('does not render a win/loss check or cancel icon — the badge background alone carries that signal', () => {
    const picks: NflPickDto[] = [
      { id: 1, userId: 'u1', userName: 'alice', team: 'KC', pick: 'Over', season: 2025, nflWeek: 19, leagueId: 1, dateCreated: '' },
    ];
    const { container } = render(
      <ThemeProvider theme={createTheme()}>
        <UserPicksMatrix
          users={['alice']}
          picks={picks}
          spreads={{ KC: { team: 'KC', isWinner: true, isOverWinner: true, isUnderWinner: false, spread: -3, over: 45, under: 45 } }}
          requiredPicks={1}
        />
      </ThemeProvider>,
    );
    expect(container.querySelector('[data-testid="CheckCircleIcon"]')).not.toBeInTheDocument();
    expect(container.querySelector('[data-testid="CancelIcon"]')).not.toBeInTheDocument();
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
    expect(screen.getAllByText('NE')).toHaveLength(1);
    expect(screen.getAllByText('KC')).toHaveLength(1);
    expect(screen.getAllByText('BUF')).toHaveLength(1);
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
