import { render, screen, within } from '@testing-library/react';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import UserPicksMatrix from '../components/UserPicksMatrix';
import type { GameView, PickView } from '../services/sportAdapter';
import { createGameView, createPickView as pick } from '../test/fixtures';

vi.mock('../components/sports/TeamLogo', () => ({
  default: ({ abbr }: { abbr: string }) => <div data-testid={`team-art-${abbr}`}>{abbr}</div>,
}));

const FUTURE = '2099-01-01T00:00:00Z';

// A game that hasn't kicked off: picks on it are revealed only to their owner, and it has no result.
const kcScheduled = createGameView({ homeTeam: 'KC', awayTeam: 'LV', homeSpread: -6.5, awaySpread: 6.5, gameStatus: 'scheduled', gameTime: FUTURE });
const kcLive = createGameView({ homeTeam: 'KC', awayTeam: 'LV', homeSpread: -6.5, awaySpread: 6.5, gameStatus: 'in_progress', homeScore: 20, awayScore: 10 });
const kcFinal = createGameView({ homeTeam: 'KC', awayTeam: 'LV', homeSpread: -6.5, awaySpread: 6.5, gameStatus: 'final', homeScore: 20, awayScore: 10 });

function renderMatrix(picks: PickView[], games: GameView[], requiredPicks = 1, mode: 'light' | 'dark' = 'light', currentUserId?: string) {
  return render(
    <ThemeProvider theme={createTheme({ palette: { mode } })}>
      <UserPicksMatrix sport="nfl" picks={picks} games={games} requiredPicks={requiredPicks} currentUserId={currentUserId} />
    </ThemeProvider>,
  );
}

const renderOne = (mode: 'light' | 'dark' = 'light') => renderMatrix([pick('alice', 'KC')], [kcScheduled], 1, mode);

describe('UserPicksMatrix — dark mode cell coloring', () => {
  // frizat: dark-mode neon logo assets are no longer used in the Matrix view at all (see the
  // "text-only badges" describe block below), but the cell background must still actually
  // change with the theme rather than staying a hardcoded light gray.
  it('uses a dark neutral cell background in dark mode, not the light-mode gray', () => {
    const { getByText } = renderOne('dark');
    const cell = getByText('KC').parentElement;
    expect(cell).toHaveStyle({ backgroundColor: 'rgb(66, 66, 66)' }); // MUI grey.800
  });

  it('keeps the light neutral cell background in light mode', () => {
    const { getByText } = renderOne('light');
    const cell = getByText('KC').parentElement;
    expect(cell).toHaveStyle({ backgroundColor: 'rgb(238, 238, 238)' }); // MUI grey.200
  });
});

describe('UserPicksMatrix — text-only badges (no team logo)', () => {
  // frizat: the helmet logo + 9px abbreviation label was hard to read at a glance in the
  // Matrix view specifically (unlike GameCard, where the logo has room to breathe) — drop the
  // logo here and show only a much larger, bold team abbreviation.
  it('does not render a team helmet/logo image in the Matrix view', () => {
    const { container } = renderOne();
    expect(container.querySelector('img')).not.toBeInTheDocument();
  });

  it('renders the team abbreviation as large, readable text', () => {
    renderOne();
    expect(screen.getByText('KC')).toHaveStyle({ fontSize: '22px', fontWeight: '800' });
  });

  it('shrinks the text for 4-character CFB abbreviations so it fits the 60px badge (e.g. UTSA, UNLV, WASH)', () => {
    // frizat: this component is shared, unmodified, by CFB's ScoresPage Matrix view — CFB
    // abbreviations run up to 4 characters, unlike NFL's 2-3.
    const utsa = createGameView({ homeTeam: 'UTSA', awayTeam: 'UNT', homeSpread: -3, awaySpread: 3, gameStatus: 'scheduled', gameTime: FUTURE });
    renderMatrix([pick('alice', 'UTSA')], [utsa]);
    expect(screen.getByText('UTSA')).toHaveStyle({ fontSize: '16px', fontWeight: '800' });
  });
});

describe('UserPicksMatrix — Over/Under shown as a bold word, not a small icon', () => {
  // frizat: a tiny corner arrow icon competing with the win/loss-colored badge background and
  // the large team text was easy to miss — spell out OVER/UNDER instead.
  it('shows the word OVER for an Over pick', () => {
    renderMatrix([pick('alice', 'KC', 'Over')], [kcScheduled]);
    expect(screen.getByText('OVER')).toBeInTheDocument();
  });

  it('shows the word UNDER for an Under pick', () => {
    renderMatrix([pick('alice', 'KC', 'Under')], [kcScheduled]);
    expect(screen.getByText('UNDER')).toBeInTheDocument();
  });

  it('does not show an OVER/UNDER label for a Spread pick', () => {
    renderOne();
    expect(screen.queryByText('OVER')).not.toBeInTheDocument();
    expect(screen.queryByText('UNDER')).not.toBeInTheDocument();
  });

  it('does not render an arrow icon anymore', () => {
    const { container } = renderMatrix([pick('alice', 'KC', 'Over')], [kcScheduled]);
    expect(container.querySelector('[data-testid="ArrowCircleUpIcon"]')).not.toBeInTheDocument();
    expect(container.querySelector('[data-testid="ArrowCircleDownIcon"]')).not.toBeInTheDocument();
  });

  // frizat: the badge background alone carries the win/loss signal; text stays a single neutral
  // ink color, same as the team abbreviation, in every case (win, loss, or no result yet).
  const overWinner = createGameView({ homeTeam: 'KC', awayTeam: 'LV', homeSpread: -6.5, awaySpread: 6.5, gameStatus: 'final', homeScore: 30, awayScore: 24 });
  it.each([
    ['light', 'a winning pick', overWinner],
    ['light', 'a losing pick', kcFinal],
    ['light', 'a pick with no result yet', kcScheduled],
    ['dark', 'a winning pick', overWinner],
    ['dark', 'a losing pick', kcFinal],
    ['dark', 'a pick with no result yet', kcScheduled],
  ] as const)('renders the OVER/UNDER label in the same neutral ink as the team name in %s mode for %s', (mode, _label, game) => {
    renderMatrix([pick('alice', 'KC', 'Over')], [game], 1, mode);
    expect(getComputedStyle(screen.getByText('OVER')).color).toBe(getComputedStyle(screen.getByText('KC')).color);
  });

  it('does not render a win/loss check or cancel icon — the badge background alone carries that signal', () => {
    const { container } = renderMatrix([pick('alice', 'KC', 'Over')], [overWinner]);
    expect(container.querySelector('[data-testid="CheckCircleIcon"]')).not.toBeInTheDocument();
    expect(container.querySelector('[data-testid="CancelIcon"]')).not.toBeInTheDocument();
  });
});

describe('UserPicksMatrix — logos mode (VITE_TEAM_ART_MODE=logos)', () => {
  afterEach(() => vi.unstubAllEnvs());

  it('renders the team logo (TeamArt) instead of text-only badges when in logos mode', () => {
    vi.stubEnv('VITE_TEAM_ART_MODE', 'logos');
    renderOne();
    expect(screen.getByTestId('team-art-KC')).toBeInTheDocument();
  });

  it('still renders text-only badges (no TeamArt) in the default badges mode', () => {
    renderOne();
    expect(screen.queryByTestId('team-art-KC')).not.toBeInTheDocument();
    expect(screen.getByText('KC')).toBeInTheDocument();
  });
});

describe('UserPicksMatrix — Over/Under is an alternate pick type, not an additional pick', () => {
  // frizat: AddPicks' server-side validation caps total picks (any type) at requiredPicks — a
  // real user's Over/Under pick REPLACES one of their spread picks. One badge per required-pick
  // column; column count always equals requiredPicks.
  const games = [
    createGameView({ homeTeam: 'NE', awayTeam: 'NYJ', homeSpread: -3, awaySpread: 3, gameStatus: 'scheduled', gameTime: FUTURE }),
    kcScheduled,
    createGameView({ homeTeam: 'BUF', awayTeam: 'MIA', homeSpread: -3, awaySpread: 3, gameStatus: 'scheduled', gameTime: FUTURE }),
  ];
  const mixedPicks = [pick('bob', 'NE', 'Over'), pick('bob', 'KC'), pick('bob', 'BUF')];

  it('renders exactly one badge per required-pick column, regardless of pick type', () => {
    renderMatrix(mixedPicks, games, 3);
    expect(screen.getAllByRole('columnheader')).toHaveLength(4); // User + Pick 1-3
    expect(screen.getAllByText('NE')).toHaveLength(1);
    expect(screen.getAllByText('KC')).toHaveLength(1);
    expect(screen.getAllByText('BUF')).toHaveLength(1);
  });

  it('column count always equals requiredPicks', () => {
    renderMatrix(mixedPicks, games, 4);
    expect(screen.getAllByRole('columnheader')).toHaveLength(5); // User + Pick 1-4
  });
});

describe('UserPicksMatrix — LIVE tag on picks in a game being played', () => {
  // A red corner dot on a green "covering" pick read as "losing"; a labeled tag says what it means.
  it('tags a live pick LIVE whether or not it is covering', () => {
    renderMatrix([pick('alice', 'KC'), pick('bob', 'LV')], [kcLive]);
    expect(screen.getAllByText('LIVE')).toHaveLength(2);
  });

  it.each([['final', kcFinal], ['not-started', kcScheduled]])('does not tag a %s pick', (_label, game) => {
    renderMatrix([pick('alice', 'KC')], [game]);
    expect(screen.queryByText('LIVE')).not.toBeInTheDocument();
  });
});

describe('UserPicksMatrix — live games are distinct from final results', () => {
  // Before this, a live game's "covering right now" value was painted the same solid green/red
  // as a final result, so a Sunday-night pick looked already decided.
  it('labels a live pick that is covering with its margin, not as covered', () => {
    renderMatrix([pick('alice', 'KC')], [kcLive]);
    expect(screen.getByLabelText('KC: covering +3.5 (live)')).toBeInTheDocument();
    expect(screen.getByText('+3.5')).toBeInTheDocument();
  });

  it('labels a live pick that is not covering with how far short it is', () => {
    renderMatrix([pick('bob', 'LV')], [kcLive]);
    expect(screen.getByLabelText('LV: not covering −3.5 (live)')).toBeInTheDocument();
    expect(screen.getByText('−3.5')).toBeInTheDocument();
  });

  it('shows the live margin next to OVER/UNDER for a totals pick', () => {
    renderMatrix([pick('alice', 'KC', 'Under')], [kcLive]); // total 30 vs 45
    expect(screen.getByLabelText('KC under: covering +15 (live)')).toBeInTheDocument();
    expect(screen.getByText('UNDER +15')).toBeInTheDocument();
  });

  it('labels final results as covered / didn\'t cover, with no margin', () => {
    renderMatrix([pick('alice', 'KC'), pick('bob', 'LV')], [kcFinal]);
    expect(screen.getByLabelText('KC: covered')).toBeInTheDocument();
    expect(screen.getByLabelText("LV: didn't cover")).toBeInTheDocument();
    expect(screen.queryByText('+3.5')).not.toBeInTheDocument();
  });
});

describe('UserPicksMatrix — who is still alive', () => {
  const buf = createGameView({ homeTeam: 'BUF', awayTeam: 'MIA', homeSpread: -3, awaySpread: 3, gameStatus: 'final', homeScore: 27, awayScore: 20 });
  const picks = [
    pick('carlos', 'MIA'), pick('carlos', 'KC'),
    pick('bob', 'BUF'), pick('bob', 'LV'),
    pick('alice', 'BUF'), pick('alice', 'KC'),
  ];

  it('marks each member alive, not covering right now, or out', () => {
    renderMatrix(picks, [buf, kcLive], 2);
    expect(screen.getByLabelText('alice: alive')).toBeInTheDocument();
    expect(screen.getByLabelText('bob: alive, but not covering right now')).toBeInTheDocument();
    expect(screen.getByLabelText('carlos: out')).toBeInTheDocument();
  });

  it('lists members still in it above members who are out', () => {
    renderMatrix(picks, [buf, kcLive], 2);
    const names = screen.getAllByRole('row').slice(1).map(r => within(r).getAllByRole('cell')[0].textContent);
    expect(names).toEqual(['alice', 'bob', 'carlos']);
  });

  it("dims an out member's picks, but not their name or status", () => {
    renderMatrix(picks, [buf, kcLive], 2);
    const carlosRow = screen.getByLabelText('carlos: out').closest('tr')!;
    const [nameCell, ...pickCells] = within(carlosRow).getAllByRole('cell');
    expect(nameCell).not.toHaveStyle({ opacity: '0.45' });
    pickCells.forEach(c => expect(c).toHaveStyle({ opacity: '0.45' }));
  });

  it('shows a lock for another member\'s pick that is hidden until kickoff', () => {
    renderMatrix([pick('eve', 'BUF')], [buf, kcScheduled], 2, 'light', 'id-alice');
    expect(screen.getByLabelText('Hidden until kickoff')).toBeInTheDocument();
  });

  it("never shows a lock on the viewer's own row", () => {
    renderMatrix([pick('alice', 'BUF')], [buf, kcScheduled], 2, 'light', 'id-alice');
    expect(screen.queryByLabelText('Hidden until kickoff')).not.toBeInTheDocument();
  });

  it('explains the live and hidden states in a legend', () => {
    renderMatrix(picks, [buf, kcLive], 2);
    expect(screen.getByText('Covering now (live)')).toBeInTheDocument();
    expect(screen.getByText('Not covering now (live)')).toBeInTheDocument();
    expect(screen.getByText('Hidden until kickoff')).toBeInTheDocument();
  });
});

describe('UserPicksMatrix — mobile sizing (frizat-2aa)', () => {
  // frizat-2aa: fixed 76x60 badges ate the whole 390px iOS viewport per column — badges shrink on
  // a mobile viewport via useMediaQuery, unchanged on desktop.
  const originalMatchMedia = window.matchMedia;

  afterEach(() => {
    window.matchMedia = originalMatchMedia;
  });

  function mockViewport(matches: boolean) {
    window.matchMedia = ((query: string) => ({
      matches,
      media: query,
      onchange: null,
      addEventListener: () => {},
      removeEventListener: () => {},
      addListener: () => {},
      removeListener: () => {},
      dispatchEvent: () => false,
    })) as typeof window.matchMedia;
  }

  it('renders a smaller badge on a mobile viewport than on desktop', () => {
    mockViewport(true);
    const { getByText } = renderOne();
    expect(getByText('KC').parentElement).toHaveStyle({ width: '44px', height: '50px' });
  });

  it('keeps the larger desktop badge size when not on a mobile viewport', () => {
    mockViewport(false);
    const { getByText } = renderOne();
    expect(getByText('KC').parentElement).toHaveStyle({ width: '60px', height: '76px' });
  });
});
