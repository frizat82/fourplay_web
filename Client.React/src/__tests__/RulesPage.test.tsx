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
  it('renders all major section headings', () => {
    renderPage();
    expect(screen.getByText(/how picks work/i)).toBeInTheDocument();
    expect(screen.getByText(/when games lock/i)).toBeInTheDocument();
    expect(screen.getByText(/playoff rounds/i)).toBeInTheDocument();
    expect(screen.getByText(/scoring/i)).toBeInTheDocument();
  });

  it('shows correct regular-season required pick count (4)', () => {
    renderPage();
    expect(screen.getByText(/4 picks/i)).toBeInTheDocument();
  });

  it('shows correct Wild Card required picks (3)', () => {
    renderPage();
    expect(screen.getByText(/wild card/i)).toBeInTheDocument();
    expect(screen.getByText(/3 picks/i)).toBeInTheDocument();
  });

  it('shows correct Divisional required picks (2)', () => {
    renderPage();
    expect(screen.getByText(/divisional/i)).toBeInTheDocument();
    expect(screen.getByText(/2 picks/i)).toBeInTheDocument();
  });

  it('references individual kickoff time, not noon CST', () => {
    renderPage();
    expect(screen.getByText(/kickoff/i)).toBeInTheDocument();
    expect(screen.queryByText(/noon cst/i)).not.toBeInTheDocument();
  });
});
