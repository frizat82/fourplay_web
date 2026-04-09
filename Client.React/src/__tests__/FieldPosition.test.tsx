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

  it('positions the ball marker at the correct percentage', () => {
    // yardLine 75 → 75% from left
    render(<FieldPosition situation={buildSituation({ yardLine: 75 })} />);
    const marker = screen.getByTestId('ball-marker');
    expect(marker).toHaveStyle({ left: '75%' });
  });
});
