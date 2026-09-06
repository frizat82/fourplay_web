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

  it('positions the ball marker at the correct percentage for away possession', () => {
    // Away team is drawn on the left, so their own-goal-relative yardLine maps straight through.
    render(<FieldPosition situation={buildSituation({ yardLine: 75, isHomePossession: false })} />);
    const marker = screen.getByTestId('ball-marker');
    expect(marker).toHaveStyle({ left: '75%' });
  });

  it('mirrors the ball marker position for home possession', () => {
    // Home team is drawn on the right, so their own goal is the right end zone: a yardLine of 9
    // (9 yards from the home team's own goal) must render near the right edge (91%), not the left.
    render(<FieldPosition situation={buildSituation({ yardLine: 9, isHomePossession: true })} />);
    const marker = screen.getByTestId('ball-marker');
    expect(marker).toHaveStyle({ left: '91%' });
  });
});
