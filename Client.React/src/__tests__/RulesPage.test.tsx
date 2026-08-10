import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { vi } from 'vitest';
import { useSportContext } from '../services/sport';
import { getNextSpreadJob } from '../services/spreadRelease';
import RulesPage from '../pages/RulesPage';

vi.mock('../services/sport', () => ({
  useSportContext: vi.fn(() => ({ sport: 'NFL', isCfb: false, isNfl: true })),
}));

vi.mock('../services/spreadRelease', () => ({
  getNextSpreadJob: vi.fn().mockResolvedValue(null),
}));

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

  it('shows correct regular-season required pick count (4)', () => {
    renderPage();
    expect(screen.getByText(/4 picks right/i)).toBeInTheDocument();
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

  it('shows all four NFL playoff rounds', () => {
    renderPage();
    expect(screen.getByText(/wild card/i)).toBeInTheDocument();
    expect(screen.getByText(/divisional/i)).toBeInTheDocument();
    expect(screen.getByText(/championship/i)).toBeInTheDocument();
    expect(screen.getByText(/super bowl/i)).toBeInTheDocument();
  });

  it('shows correct Wild Card required picks (3)', () => {
    renderPage();
    const wildCardSection = screen.getByText(/wild card/i).closest('div');
    expect(wildCardSection).toHaveTextContent('3');
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

describe('RulesPage — CFB playoff grid', () => {
  beforeEach(() => {
    vi.mocked(useSportContext).mockReturnValue({ sport: 'CFB', isCfb: true, isNfl: false });
  });

  it('shows CFB subtitle', () => {
    renderPage();
    expect(screen.getByText(/fewer picks in the cfp/i)).toBeInTheDocument();
  });

  it('shows Conf. Championships with 4 picks', () => {
    renderPage();
    const section = screen.getByText(/conf\. championships/i).closest('div');
    expect(section).toHaveTextContent('4');
  });

  it('shows First Round with 3 picks', () => {
    renderPage();
    const section = screen.getByText(/first round/i).closest('div');
    expect(section).toHaveTextContent('3');
  });

  it('shows Quarterfinals with 3 picks', () => {
    renderPage();
    const section = screen.getByText(/quarterfinals/i).closest('div');
    expect(section).toHaveTextContent('3');
  });

  it('shows Championship with 1 pick', () => {
    renderPage();
    const sections = screen.getAllByText(/championship/i);
    // Multiple "championship" matches — find the one with picks info
    const pickSection = sections.find((el) => el.closest('div')?.textContent?.includes('pick required'));
    expect(pickSection?.closest('div')).toHaveTextContent('1');
  });

  it('shows 5 CFB rounds, not 4 NFL rounds', () => {
    renderPage();
    expect(screen.getByText(/conf\. championships/i)).toBeInTheDocument();
    expect(screen.getByText(/first round/i)).toBeInTheDocument();
    expect(screen.getByText(/quarterfinals/i)).toBeInTheDocument();
    expect(screen.getByText(/semifinals/i)).toBeInTheDocument();
    // NFL-specific rounds should not appear
    expect(screen.queryByText(/wild card/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/super bowl/i)).not.toBeInTheDocument();
  });
});

describe('RulesPage — spread lock section', () => {
  beforeEach(() => {
    vi.mocked(getNextSpreadJob).mockReset();
  });

  it('shows the "When spreads lock" section', () => {
    vi.mocked(useSportContext).mockReturnValue({ sport: 'NFL', isCfb: false, isNfl: true });
    vi.mocked(getNextSpreadJob).mockResolvedValue(null);
    renderPage();
    expect(screen.getByText(/when spreads lock/i)).toBeInTheDocument();
  });

  it('explains the distinction between spread lock and pick lock', () => {
    vi.mocked(useSportContext).mockReturnValue({ sport: 'NFL', isCfb: false, isNfl: true });
    vi.mocked(getNextSpreadJob).mockResolvedValue(null);
    renderPage();
    expect(screen.getByText(/freezes once for the whole week/i)).toBeInTheDocument();
  });

  it('calls getNextSpreadJob with NFL for the NFL site', () => {
    vi.mocked(useSportContext).mockReturnValue({ sport: 'NFL', isCfb: false, isNfl: true });
    vi.mocked(getNextSpreadJob).mockResolvedValue(null);
    renderPage();
    expect(getNextSpreadJob).toHaveBeenCalledWith('NFL');
  });

  it('calls getNextSpreadJob with CFB for the CFB site', () => {
    vi.mocked(useSportContext).mockReturnValue({ sport: 'CFB', isCfb: true, isNfl: false });
    vi.mocked(getNextSpreadJob).mockResolvedValue(null);
    renderPage();
    expect(getNextSpreadJob).toHaveBeenCalledWith('CFB');
  });

  it('shows the next lock time once loaded', async () => {
    vi.mocked(useSportContext).mockReturnValue({ sport: 'NFL', isCfb: false, isNfl: true });
    vi.mocked(getNextSpreadJob).mockResolvedValue('2026-09-09T14:20:00Z');
    renderPage();
    await waitFor(() => {
      expect(screen.getByText(/next lock:/i)).toBeInTheDocument();
    });
  });

  it('does not show "Next lock" text before the fetch resolves', () => {
    vi.mocked(useSportContext).mockReturnValue({ sport: 'NFL', isCfb: false, isNfl: true });
    vi.mocked(getNextSpreadJob).mockReturnValue(new Promise(() => {})); // never resolves
    renderPage();
    expect(screen.queryByText(/next lock:/i)).not.toBeInTheDocument();
  });
});
