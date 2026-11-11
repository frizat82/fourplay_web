import { describe, it, expect, vi, afterEach } from 'vitest';
import { act, fireEvent, render, screen } from '@testing-library/react';
import WeekWinCelebration from '../components/WeekWinCelebration';
import type { CelebrationPick } from '../utils/weekWin';

vi.mock('../components/sports/TeamArt', () => ({
  default: ({ abbr }: { abbr: string }) => <div data-testid={`team-art-${abbr}`}>{abbr}</div>,
}));

const picks: CelebrationPick[] = [
  { team: 'DAL', pickType: 'Spread', margin: 10.5 },
  { team: 'DET', pickType: 'Spread', margin: 4.5 },
  { team: 'KC', pickType: 'Over', margin: 3 },
  { team: 'SEA', pickType: 'Spread', margin: null },
];

function renderCelebration(overrides: Partial<Parameters<typeof WeekWinCelebration>[0]> = {}) {
  const onDone = vi.fn();
  render(
    <WeekWinCelebration sport="nfl" leagueName="Sunday Sweats" weekLabel="Week 9" amount={35} picks={picks} onDone={onDone} {...overrides} />,
  );
  return onDone;
}

/** Makes the reduced-motion media query match for one test. */
function preferReducedMotion() {
  const original = window.matchMedia;
  window.matchMedia = ((query: string) => ({
    ...original(query),
    matches: query.includes('prefers-reduced-motion: reduce'),
  })) as typeof window.matchMedia;
  return () => { window.matchMedia = original; };
}

describe('WeekWinCelebration', () => {
  afterEach(() => vi.useRealTimers());

  it('announces the win with the league, the week and what the viewer collects', () => {
    renderCelebration();
    const dialog = screen.getByRole('dialog', { name: 'You won the week!' });
    expect(dialog).toHaveTextContent('Sunday Sweats');
    expect(dialog).toHaveTextContent('Week 9');
    expect(dialog).toHaveTextContent('4 for 4');
    expect(screen.getByLabelText('You collect $35')).toBeInTheDocument();
  });

  it('shows each covered pick with how far it cleared its line', () => {
    renderCelebration();
    expect(screen.getByRole('img', { name: 'DAL covered by 10.5' })).toHaveTextContent('+10.5');
    expect(screen.getByRole('img', { name: 'KC Over covered by 3' })).toHaveTextContent('Over +3');
    expect(screen.getByRole('img', { name: 'SEA covered' })).toBeInTheDocument();
  });

  it('closes on a tap anywhere', () => {
    const onDone = renderCelebration();
    fireEvent.click(screen.getByText('You won the week!'));
    expect(onDone).toHaveBeenCalledTimes(1);
  });

  it('closes once from the Continue button, and on Escape', () => {
    const onDone = renderCelebration();
    fireEvent.click(screen.getByRole('button', { name: 'Continue' }));
    expect(onDone).toHaveBeenCalledTimes(1);
    fireEvent.keyDown(screen.getByRole('dialog'), { key: 'Escape' });
    expect(onDone).toHaveBeenCalledTimes(2);
  });

  it('counts the winnings up to the final amount', () => {
    vi.useFakeTimers();
    renderCelebration();
    expect(screen.getByLabelText('You collect $35')).not.toHaveTextContent('+$35');
    act(() => { vi.advanceTimersByTime(10_000); });
    expect(screen.getByLabelText('You collect $35')).toHaveTextContent('+$35');
  });

  it('shows the final amount at once, with no confetti, when the viewer prefers reduced motion', () => {
    const restore = preferReducedMotion();
    try {
      renderCelebration();
      expect(screen.getByLabelText('You collect $35')).toHaveTextContent('+$35');
      expect(screen.queryByTestId('confetti')).not.toBeInTheDocument();
    } finally {
      restore();
    }
  });

  it('plays confetti otherwise', () => {
    renderCelebration();
    expect(screen.getByTestId('confetti')).toBeInTheDocument();
  });

  // Everyone covering is a push: nobody pays, and the pot carries to the next week.
  it('says the pot rolls over, with no dollar amount, when everyone won', () => {
    renderCelebration({ amount: 0 });
    expect(screen.getByText(/pot rolls over/i)).toBeInTheDocument();
    expect(screen.queryByText(/\$0/)).not.toBeInTheDocument();
  });

  it('still celebrates when the picks could not be loaded', () => {
    renderCelebration({ picks: [] });
    expect(screen.getByRole('dialog', { name: 'You won the week!' })).toBeInTheDocument();
    expect(screen.queryByText(/for 0/)).not.toBeInTheDocument();
    expect(screen.getByLabelText('You collect $35')).toBeInTheDocument();
  });
});
