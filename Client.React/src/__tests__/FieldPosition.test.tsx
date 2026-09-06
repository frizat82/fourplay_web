import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import FieldPosition from '../components/FieldPosition';
import type { GameSituation } from '../types/liveGame';

function buildSituation(overrides?: Partial<GameSituation>): GameSituation {
  return {
    possessionTeam: 'BUF',
    isHomePossession: false,
    yardLine: 40,
    down: 1,
    distance: 10,
    isRedZone: false,
    downDistanceText: '1st & 10 at BUF 40',
    ...overrides,
  };
}

describe('FieldPosition', () => {
  it('renders a placeholder element (not null) when situation is null', () => {
    const { container } = render(<FieldPosition situation={null} />);
    expect(container.firstChild).not.toBeNull();
    expect(screen.queryByTestId('field-position-bar')).toBeNull();
  });

  it('renders down and distance text', () => {
    render(<FieldPosition situation={buildSituation({ downDistanceText: '3rd & 5 at KC 25' })} />);
    expect(screen.getByText('3rd & 5 at KC 25')).toBeInTheDocument();
  });

  it('shows right-pointing arrow when away team has possession (attacking right)', () => {
    render(<FieldPosition situation={buildSituation({ isHomePossession: false })} />);
    expect(screen.getByTestId('possession-arrow')).toHaveTextContent('▶');
  });

  it('shows left-pointing arrow when home team has possession (attacking left)', () => {
    render(<FieldPosition situation={buildSituation({ isHomePossession: true })} />);
    expect(screen.getByTestId('possession-arrow')).toHaveTextContent('◀');
  });

  it('applies red zone styling when isRedZone is true', () => {
    render(<FieldPosition situation={buildSituation({ isRedZone: true })} />);
    expect(screen.getByTestId('field-position-bar')).toHaveAttribute('data-redzone', 'true');
  });

  it('does not apply red zone styling when isRedZone is false', () => {
    render(<FieldPosition situation={buildSituation({ isRedZone: false })} />);
    expect(screen.getByTestId('field-position-bar')).toHaveAttribute('data-redzone', 'false');
  });

  it('positions the ball marker at 100 - yardLine regardless of home possession', () => {
    // Home team is drawn on the right, so their own goal is the right end zone: a yardLine of 9
    // (9 yards from the home team's own goal) must render near the right edge (91%), not the left.
    render(<FieldPosition situation={buildSituation({ yardLine: 9, isHomePossession: true })} />);
    const marker = screen.getByTestId('ball-marker');
    expect(marker).toHaveStyle({ left: '91%' });
  });

  it('positions the ball marker at 100 - yardLine regardless of away possession', () => {
    // Confirmed against a live game: away team (Clemson) with the ball at their own 33 was sent
    // to the frontend as yardLine=67 (distance from the HOME team's goal, not the possessor's) —
    // the previous "away possession maps yardLine straight through" test asserted the bug's own
    // behavior as correct. yardLine is a fixed home-relative coordinate; possession never changes
    // the position formula, only the arrow direction.
    render(<FieldPosition situation={buildSituation({ yardLine: 67, isHomePossession: false })} />);
    const marker = screen.getByTestId('ball-marker');
    expect(marker).toHaveStyle({ left: '33%' });
  });

  it('matches real captured ESPN wire data: away team driving into home territory', () => {
    // sample_espn_nfl.json (real Super Bowl LX payload, repo root): NE is home, SEA is away and
    // holds possession ("possession": "26" = SEA's team id), yet downDistanceText names NE ("2nd
    // & 7 at NE 35") because SEA has driven into NE's territory. yardLine=35 only makes sense as
    // "35 yards from NE's (home's) own goal" — proving yardLine is home-relative regardless of
    // who has the ball, not offense-relative. Correct position: 100 - 35 = 65% (near home's side,
    // where NE's goal is under threat).
    render(<FieldPosition situation={buildSituation({
      yardLine: 35,
      isHomePossession: false,
      downDistanceText: '2nd & 7 at NE 35',
    })} />);
    const marker = screen.getByTestId('ball-marker');
    expect(marker).toHaveStyle({ left: '65%' });
  });
});
