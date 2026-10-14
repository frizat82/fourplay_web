import { render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { vi } from 'vitest';
import { useSportContext } from '../services/sport';
import { getNflSpreadLockSchedule } from '../api/league';
import { getCfbSpreadLockSchedule } from '../api/cfb';
import RulesPage from '../pages/RulesPage';

vi.mock('../services/sport', () => ({
  useSportContext: vi.fn(() => ({ sport: 'NFL', isCfb: false, isNfl: true })),
}));

vi.mock('../api/league', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../api/league')>()),
  getNflSpreadLockSchedule: vi.fn().mockResolvedValue([]),
}));

vi.mock('../api/cfb', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../api/cfb')>()),
  getCfbSpreadLockSchedule: vi.fn().mockResolvedValue([]),
}));

// The season's control-table rows, as the spread-lock-schedule endpoints serve them — every pick
// count on the page comes from these (frizat-d47), so a test that wants a number supplies the row.
const lock = '2099-01-01T00:00:00Z';
const NFL_SCHEDULE = [
  { weekLabel: 'Week 1', spreadLockDatetime: lock, isPostSeason: false, requiredPicks: 4 },
  { weekLabel: 'Wild Card Weekend', spreadLockDatetime: lock, isPostSeason: true, requiredPicks: 3 },
  { weekLabel: 'Divisional Round', spreadLockDatetime: lock, isPostSeason: true, requiredPicks: 3 },
  { weekLabel: 'Conference Championships', spreadLockDatetime: lock, isPostSeason: true, requiredPicks: 2 },
  { weekLabel: 'Super Bowl LXI', spreadLockDatetime: lock, isPostSeason: true, requiredPicks: 1 },
];
const CFB_SCHEDULE = [
  { weekLabel: 'Week 1', spreadLockDatetime: lock, isPostSeason: false, requiredPicks: 4 },
  { weekLabel: 'Conf. Championships', spreadLockDatetime: lock, isPostSeason: true, requiredPicks: 4 },
  { weekLabel: 'CFP First Round', spreadLockDatetime: lock, isPostSeason: true, requiredPicks: 3 },
  { weekLabel: 'CFP Quarterfinals', spreadLockDatetime: lock, isPostSeason: true, requiredPicks: 3 },
  { weekLabel: 'CFP Semifinals', spreadLockDatetime: lock, isPostSeason: true, requiredPicks: 2 },
  { weekLabel: 'CFP Championship', spreadLockDatetime: lock, isPostSeason: true, requiredPicks: 1 },
];

beforeEach(() => {
  vi.mocked(getNflSpreadLockSchedule).mockReset().mockResolvedValue(NFL_SCHEDULE);
  vi.mocked(getCfbSpreadLockSchedule).mockReset().mockResolvedValue(CFB_SCHEDULE);
});

function renderPage() {
  return render(
    <MemoryRouter>
      <RulesPage />
    </MemoryRouter>,
  );
}

describe('RulesPage — NFL', () => {
  beforeEach(() => {
    vi.mocked(useSportContext).mockReturnValue({ sport: 'NFL', isCfb: false, isNfl: true });
  });

  it('renders the page title', () => {
    renderPage();
    expect(screen.getByText(/how iv league works/i)).toBeInTheDocument();
  });

  it('explains the league-configured tease mechanic', () => {
    renderPage();
    expect(screen.getByText(/configured amount/i)).toBeInTheDocument();
    expect(screen.getByText(/tease pts/i)).toBeInTheDocument();
  });

  it('shows the tease formula chips', () => {
    renderPage();
    expect(screen.getAllByText(/vegas line/i)[0]).toBeInTheDocument();
    expect(screen.getAllByText(/your line/i)[0]).toBeInTheDocument();
  });

  it('shows the Seattle vs Chicago matchup example', () => {
    renderPage();
    expect(screen.getAllByText(/seattle seahawks/i)[0]).toBeInTheDocument();
    expect(screen.getAllByText(/chicago bears/i)[0]).toBeInTheDocument();
  });

  it('shows teased spreads for both teams', () => {
    renderPage();
    expect(screen.getByText('+8.5')).toBeInTheDocument();
    expect(screen.getByText('+17.5')).toBeInTheDocument();
  });

  it('shows winner and loser chips in the scenario', () => {
    renderPage();
    expect(screen.getByText('Winner')).toBeInTheDocument();
    expect(screen.getByText('Loser')).toBeInTheDocument();
  });

  it('shows correct regular-season required pick count (4)', async () => {
    renderPage();
    expect(await screen.findByText(/4 picks right/i)).toBeInTheDocument();
  });

  it('explains both-sides picking strategy', () => {
    renderPage();
    expect(screen.getByText(/both sides/i)).toBeInTheDocument();
  });

  it('explains the juice mechanic', () => {
    renderPage();
    expect(screen.getAllByText(/juice/i)[0]).toBeInTheDocument();
    expect(screen.getAllByText(/earn the juice/i)[0]).toBeInTheDocument();
  });

  it('shows all four NFL playoff rounds', async () => {
    renderPage();
    const grid = within(await screen.findByTestId('playoff-grid'));
    expect(grid.getByText(/wild card/i)).toBeInTheDocument();
    expect(grid.getByText(/divisional/i)).toBeInTheDocument();
    expect(grid.getByText(/championship/i)).toBeInTheDocument();
    expect(grid.getByText(/super bowl/i)).toBeInTheDocument();
  });

  it('shows correct Wild Card required picks (3)', async () => {
    renderPage();
    const wildCardSection = within(await screen.findByTestId('playoff-grid')).getByText(/wild card/i).closest('div');
    expect(wildCardSection).toHaveTextContent('3');
  });

  // frizat-d47: the numbers are the rows', not a rule baked into the page.
  it('takes every pick count from the season\'s rows', async () => {
    vi.mocked(getNflSpreadLockSchedule).mockResolvedValue([
      { weekLabel: 'Week 1', spreadLockDatetime: lock, isPostSeason: false, requiredPicks: 5 },
      { weekLabel: 'Wild Card Weekend', spreadLockDatetime: lock, isPostSeason: true, requiredPicks: 2 },
    ]);
    renderPage();
    expect(await screen.findByText(/5 picks right/i)).toBeInTheDocument();
    const wildCard = within(screen.getByTestId('playoff-grid')).getByText(/wild card/i).closest('div');
    expect(wildCard).toHaveTextContent('2');
  });

  it('leaves the playoff grid out until the season\'s rows load', () => {
    vi.mocked(getNflSpreadLockSchedule).mockReturnValue(new Promise(() => {}));
    renderPage();
    expect(screen.queryByTestId('playoff-grid')).not.toBeInTheDocument();
  });

  it('references individual kickoff time, not all-at-once locking', () => {
    renderPage();
    expect(screen.getByText(/kickoff time/i)).toBeInTheDocument();
    expect(screen.queryByText(/noon cst/i)).not.toBeInTheDocument();
  });

  it('mentions server-side enforcement of deadlines', () => {
    renderPage();
    expect(screen.getByText(/enforced server-side/i)).toBeInTheDocument();
  });
});

describe('RulesPage — Over/Under in the postseason', () => {
  it('explains Over/Under replaces a pick rather than adding one (NFL)', async () => {
    vi.mocked(useSportContext).mockReturnValue({ sport: 'NFL', isCfb: false, isNfl: true });
    renderPage();
    expect(screen.getByText(/over\/under in the postseason/i)).toBeInTheDocument();
    expect(screen.getByText(/replaces/i)).toBeInTheDocument();
    expect(await screen.findByText(/not 4/i)).toBeInTheDocument();
  });

  it('shows an NFL example with exactly one Over/Under pick among the required picks', async () => {
    vi.mocked(useSportContext).mockReturnValue({ sport: 'NFL', isCfb: false, isNfl: true });
    renderPage();
    const example = within(await screen.findByTestId('over-under-example'));
    expect(example.getByText(/wild card weekend · 3 picks required/i)).toBeInTheDocument();
    expect(example.getAllByText('Spread')).toHaveLength(2);
    expect(example.getAllByText('Over/Under')).toHaveLength(1);
  });

  it('shows a CFB example with exactly one Over/Under pick among the required picks', async () => {
    vi.mocked(useSportContext).mockReturnValue({ sport: 'CFB', isCfb: true, isNfl: false });
    renderPage();
    const example = within(await screen.findByTestId('over-under-example'));
    expect(example.getByText(/cfp first round · 3 picks required/i)).toBeInTheDocument();
    expect(example.getAllByText('Spread')).toHaveLength(2);
    expect(example.getAllByText('Over/Under')).toHaveLength(1);
    expect(screen.getByText(/not 4/i)).toBeInTheDocument();
  });
});

describe('RulesPage — CFB playoff grid', () => {
  beforeEach(() => {
    vi.mocked(useSportContext).mockReturnValue({ sport: 'CFB', isCfb: true, isNfl: false });
  });

  it('shows CFB subtitle', () => {
    renderPage();
    expect(screen.getByText(/fewer picks in the cfp/i)).toBeInTheDocument();
  });

  it('shows Conf. Championships with 4 picks', async () => {
    renderPage();
    await screen.findByTestId('playoff-grid');
    const section = within(screen.getByTestId('playoff-grid')).getByText(/conf\. championships/i).closest('div');
    expect(section).toHaveTextContent('4');
  });

  it('shows First Round with 3 picks', async () => {
    renderPage();
    await screen.findByTestId('playoff-grid');
    const section = within(screen.getByTestId('playoff-grid')).getByText(/first round/i).closest('div');
    expect(section).toHaveTextContent('3');
  });

  it('shows Quarterfinals with 3 picks', async () => {
    renderPage();
    await screen.findByTestId('playoff-grid');
    const section = within(screen.getByTestId('playoff-grid')).getByText(/quarterfinals/i).closest('div');
    expect(section).toHaveTextContent('3');
  });

  it('shows Championship with 1 pick', async () => {
    renderPage();
    await screen.findByTestId('playoff-grid');
    const sections = screen.getAllByText(/championship/i);
    // Multiple "championship" matches — find the one with picks info
    const pickSection = sections.find((el) => el.closest('div')?.textContent?.includes('pick required'));
    expect(pickSection?.closest('div')).toHaveTextContent('1');
  });

  it('shows 5 CFB rounds, not 4 NFL rounds', async () => {
    renderPage();
    await screen.findByTestId('playoff-grid');
    const grid = within(screen.getByTestId('playoff-grid'));
    expect(grid.getByText(/conf\. championships/i)).toBeInTheDocument();
    expect(grid.getByText(/first round/i)).toBeInTheDocument();
    expect(grid.getByText(/quarterfinals/i)).toBeInTheDocument();
    expect(grid.getByText(/semifinals/i)).toBeInTheDocument();
    // NFL-specific rounds should not appear anywhere on the page
    expect(screen.queryByText(/wild card/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/super bowl/i)).not.toBeInTheDocument();
  });
});

describe('RulesPage — spread lock section', () => {

  it('shows the "When spreads lock" section', () => {
    vi.mocked(useSportContext).mockReturnValue({ sport: 'NFL', isCfb: false, isNfl: true });
    renderPage();
    expect(screen.getByText(/when spreads lock/i)).toBeInTheDocument();
  });

  it('explains the distinction between spread lock and pick lock', () => {
    vi.mocked(useSportContext).mockReturnValue({ sport: 'NFL', isCfb: false, isNfl: true });
    renderPage();
    expect(screen.getByText(/freezes once for the whole week/i)).toBeInTheDocument();
  });

  it('fetches the NFL schedule, not CFB, on the NFL site', () => {
    vi.mocked(useSportContext).mockReturnValue({ sport: 'NFL', isCfb: false, isNfl: true });
    renderPage();
    expect(getNflSpreadLockSchedule).toHaveBeenCalled();
    expect(getCfbSpreadLockSchedule).not.toHaveBeenCalled();
  });

  it('fetches the CFB schedule, not NFL, on the CFB site', () => {
    vi.mocked(useSportContext).mockReturnValue({ sport: 'CFB', isCfb: true, isNfl: false });
    renderPage();
    expect(getCfbSpreadLockSchedule).toHaveBeenCalled();
    expect(getNflSpreadLockSchedule).not.toHaveBeenCalled();
  });

  it('shows every week with its lock time once loaded', async () => {
    vi.mocked(useSportContext).mockReturnValue({ sport: 'NFL', isCfb: false, isNfl: true });
    vi.mocked(getNflSpreadLockSchedule).mockResolvedValue([
      { weekLabel: 'Week 1', spreadLockDatetime: '2026-09-02T14:20:00Z', isPostSeason: false, requiredPicks: 4 },
      { weekLabel: 'Week 2', spreadLockDatetime: '2026-09-09T14:20:00Z', isPostSeason: false, requiredPicks: 4 },
    ]);
    renderPage();
    await waitFor(() => {
      expect(screen.getByText('Week 1')).toBeInTheDocument();
      expect(screen.getByText('Week 2')).toBeInTheDocument();
    });
  });

  it('does not render the schedule section before the fetch resolves', () => {
    vi.mocked(useSportContext).mockReturnValue({ sport: 'NFL', isCfb: false, isNfl: true });
    vi.mocked(getNflSpreadLockSchedule).mockReturnValue(new Promise(() => {})); // never resolves
    renderPage();
    expect(screen.queryByText('Week 1')).not.toBeInTheDocument();
  });

  it('highlights the soonest week that has not locked yet', async () => {
    vi.mocked(useSportContext).mockReturnValue({ sport: 'NFL', isCfb: false, isNfl: true });
    vi.mocked(getNflSpreadLockSchedule).mockResolvedValue([
      { weekLabel: 'Past Week', spreadLockDatetime: '2020-01-01T00:00:00Z', isPostSeason: false, requiredPicks: 4 },
      { weekLabel: 'Upcoming Week', spreadLockDatetime: '2099-01-01T00:00:00Z', isPostSeason: false, requiredPicks: 4 },
    ]);
    renderPage();

    const upcoming = await screen.findByText('Upcoming Week');
    const past = screen.getByText('Past Week');
    // MUI applies fontWeight via inline style on the Typography — 600 for the highlighted row,
    // 400 for every other row.
    expect(upcoming).toHaveStyle({ fontWeight: 600 });
    expect(past).toHaveStyle({ fontWeight: 400 });
  });

  it('shows a "Next" chip only on the highlighted upcoming week', async () => {
    vi.mocked(useSportContext).mockReturnValue({ sport: 'NFL', isCfb: false, isNfl: true });
    vi.mocked(getNflSpreadLockSchedule).mockResolvedValue([
      { weekLabel: 'Past Week', spreadLockDatetime: '2020-01-01T00:00:00Z', isPostSeason: false, requiredPicks: 4 },
      { weekLabel: 'Upcoming Week', spreadLockDatetime: '2099-01-01T00:00:00Z', isPostSeason: false, requiredPicks: 4 },
    ]);
    renderPage();

    await screen.findByText('Upcoming Week');
    expect(screen.getByText('Next')).toBeInTheDocument();
    // Exactly one "Next" chip — not one per row.
    expect(screen.getAllByText('Next')).toHaveLength(1);
  });
});
