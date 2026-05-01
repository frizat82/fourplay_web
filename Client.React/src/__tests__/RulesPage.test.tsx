import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import RulesPage from '../pages/RulesPage';

function renderPage() {
  return render(
    <MemoryRouter>
      <RulesPage />
    </MemoryRouter>,
  );
}

describe('RulesPage', () => {
  it('renders the page title', () => {
    renderPage();
    expect(screen.getByText(/how fourplay works/i)).toBeInTheDocument();
  });

  it('explains the 13-point tease mechanic', () => {
    renderPage();
    expect(screen.getByText(/13 points/i)).toBeInTheDocument();
    expect(screen.getByText(/13 pt tease/i)).toBeInTheDocument();
  });

  it('shows the tease formula chips', () => {
    renderPage();
    expect(screen.getByText(/vegas line/i)).toBeInTheDocument();
    expect(screen.getByText(/your line/i)).toBeInTheDocument();
  });

  it('shows the Seattle vs Chicago matchup example', () => {
    renderPage();
    expect(screen.getByText(/seattle seahawks/i)).toBeInTheDocument();
    expect(screen.getByText(/chicago bears/i)).toBeInTheDocument();
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
    expect(screen.getByText(/juice/i)).toBeInTheDocument();
    expect(screen.getByText(/earn the juice/i)).toBeInTheDocument();
  });

  it('shows all four playoff rounds', () => {
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
