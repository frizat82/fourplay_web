import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi } from 'vitest';
import GameCard from '../components/sports/GameCard';

vi.mock('../components/sports/TeamHelmet', () => ({
  default: ({ abbr }: { abbr: string }) => <div data-testid={`helmet-${abbr}`}>{abbr}</div>,
}));

vi.mock('../components/WeatherIcon', () => ({
  default: ({ iconKey, temperatureF }: { iconKey?: string | null; temperatureF?: number | null }) =>
    iconKey ? <div data-testid="weather-icon">{temperatureF}°</div> : null,
}));

const baseProps = {
  homeTeam: 'KC',
  awayTeam: 'BUF',
  homeSpread: -3,
  awaySpread: 3,
  overUnder: 51.5,
  gameTime: '2023-10-22T17:00:00Z',
  mode: 'pick' as const,
};

describe('GameCard', () => {
  it('renders without optional props (backward compat)', () => {
    render(<GameCard {...baseProps} />);
    expect(screen.getByTestId('helmet-KC')).toBeInTheDocument();
    expect(screen.getByTestId('helmet-BUF')).toBeInTheDocument();
    expect(screen.queryByTestId('weather-icon')).not.toBeInTheDocument();
    expect(screen.queryByText('8-2')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /over/i })).not.toBeInTheDocument();
  });

  it('renders WeatherIcon when weatherDisplayValue provided', () => {
    render(
      <GameCard
        {...baseProps}
        weatherDisplayValue="Partly Cloudy"
        weatherConditionId="3"
        weatherTemperatureF={55}
      />
    );
    expect(screen.getByTestId('weather-icon')).toBeInTheDocument();
    expect(screen.getByText('55°')).toBeInTheDocument();
  });

  it('does not render weather when weatherDisplayValue is absent', () => {
    render(<GameCard {...baseProps} />);
    expect(screen.queryByTestId('weather-icon')).not.toBeInTheDocument();
  });

  it('renders homeRecord and awayRecord when not postSeason', () => {
    render(
      <GameCard {...baseProps} homeRecord="8-2" awayRecord="5-5" isPostSeason={false} />
    );
    expect(screen.getByText('8-2')).toBeInTheDocument();
    expect(screen.getByText('5-5')).toBeInTheDocument();
  });

  it('renders homeRank and awayRank when a team is ranked', () => {
    render(<GameCard {...baseProps} homeRank={3} awayRank={17} />);
    expect(screen.getByText('#3')).toBeInTheDocument();
    expect(screen.getByText('#17')).toBeInTheDocument();
  });

  it('does not render a rank badge for an unranked team', () => {
    render(<GameCard {...baseProps} homeRank={null} awayRank={undefined} />);
    expect(screen.queryByText(/^#/)).not.toBeInTheDocument();
  });

  it('suppresses records when isPostSeason=true', () => {
    render(
      <GameCard {...baseProps} homeRecord="8-2" awayRecord="5-5" isPostSeason={true} />
    );
    expect(screen.queryByText('8-2')).not.toBeInTheDocument();
    expect(screen.queryByText('5-5')).not.toBeInTheDocument();
  });

  it('renders jersey img instead of helmet when homeJerseyUrl provided', () => {
    render(<GameCard {...baseProps} homeJerseyUrl="https://example.com/jersey.png" />);
    const img = screen.getByRole('img', { name: /KC/i });
    expect(img).toHaveAttribute('src', 'https://example.com/jersey.png');
  });

  it('renders postseason Over and Under buttons when isPostSeason=true in pick mode', () => {
    render(
      <GameCard
        {...baseProps}
        isPostSeason={true}
        overValue={51.5}
        underValue={51.5}
        overPickState="none"
        underPickState="none"
        onPickOver={vi.fn()}
        onPickUnder={vi.fn()}
      />
    );
    expect(screen.getByRole('button', { name: /^over$/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /^under$/i })).toBeInTheDocument();
  });

  it('does not render O/U panel when isPostSeason=false', () => {
    render(<GameCard {...baseProps} isPostSeason={false} overValue={51.5} underValue={51.5} />);
    expect(screen.queryByRole('button', { name: /^over$/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^under$/i })).not.toBeInTheDocument();
  });

  it('shows "Over 51.5 ✓" when overPickState=submitted', () => {
    render(
      <GameCard
        {...baseProps}
        isPostSeason={true}
        overValue={51.5}
        underValue={51.5}
        overPickState="submitted"
        underPickState="none"
        onPickOver={vi.fn()}
        onPickUnder={vi.fn()}
      />
    );
    expect(screen.getByRole('button', { name: /over 51\.5 ✓/i })).toBeInTheDocument();
  });

  it('calls onPickHome when Pick button clicked', async () => {
    const user = userEvent.setup();
    const onPickHome = vi.fn();
    render(<GameCard {...baseProps} homePickState="none" onPickHome={onPickHome} />);
    await user.click(screen.getByRole('button', { name: /Pick KC/i }));
    expect(onPickHome).toHaveBeenCalledOnce();
  });

  // ── mon.8: submitted state, aria-labels, team abbr text, O/U copy ──────────

  it('submitted away pick is disabled with "Locked in" label', () => {
    render(<GameCard {...baseProps} awayPickState="submitted" />);
    const btn = screen.getByRole('button', { name: /BUF locked in/i });
    expect(btn).toBeDisabled();
  });

  it('submitted home pick is disabled with "Locked in" label', () => {
    render(<GameCard {...baseProps} homePickState="submitted" />);
    const btn = screen.getByRole('button', { name: /KC locked in/i });
    expect(btn).toBeDisabled();
  });

  // frizat: MUI's disabled state flattens every contained button to the same uniform gray
  // regardless of `color`, erasing the picked/not-picked distinction the instant a game locks —
  // the most common state once a week is underway. A locked "Picked" button should stay a filled
  // success (green) button, not fall back to MUI's default disabled gray class.
  it('a locked "Picked" button keeps its filled success styling, not the default disabled gray', () => {
    render(<GameCard {...baseProps} homePickState="submitted" />);
    const btn = screen.getByRole('button', { name: /KC locked in/i });
    expect(btn).toHaveClass('MuiButton-contained', 'MuiButton-containedSuccess');
  });

  // A locked, never-picked button reads as a filled info (blue) button — distinct from the
  // filled success (green) "Picked" button, and not the hard-to-read amber/warning color.
  it('a locked, never-picked button keeps its filled info styling, not the default disabled gray', () => {
    render(<GameCard {...baseProps} awayPickState="none" locked />);
    const btn = screen.getByRole('button', { name: /^Pick BUF$/i });
    expect(btn).toBeDisabled();
    expect(btn).toHaveClass('MuiButton-contained', 'MuiButton-containedInfo');
  });

  it('unpicked away team button has aria-label "Pick BUF"', () => {
    render(<GameCard {...baseProps} awayPickState="none" />);
    expect(screen.getByRole('button', { name: /^Pick BUF$/i })).toBeInTheDocument();
  });

  it('unpicked home team button has aria-label "Pick KC"', () => {
    render(<GameCard {...baseProps} homePickState="none" />);
    expect(screen.getByRole('button', { name: /^Pick KC$/i })).toBeInTheDocument();
  });

  it('team abbreviation rendered as visible text even when jerseyUrl is provided', () => {
    render(
      <GameCard
        {...baseProps}
        awayJerseyUrl="https://example.com/buf.png"
        homeJerseyUrl="https://example.com/kc.png"
      />
    );
    expect(screen.getAllByText('BUF').length).toBeGreaterThan(0);
    expect(screen.getAllByText('KC').length).toBeGreaterThan(0);
  });

  it('O/U unpicked still reads "Over" and "Under"', () => {
    render(
      <GameCard
        {...baseProps}
        isPostSeason={true}
        overValue={47.5}
        underValue={47.5}
        overPickState="none"
        underPickState="none"
        onPickOver={vi.fn()}
        onPickUnder={vi.fn()}
      />
    );
    expect(screen.getByRole('button', { name: /^Over$/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /^Under$/i })).toBeInTheDocument();
  });

  it('O/U picked under shows "Under 47.5 ✓"', () => {
    render(
      <GameCard
        {...baseProps}
        isPostSeason={true}
        overValue={47.5}
        underValue={47.5}
        overPickState="none"
        underPickState="submitted"
        onPickOver={vi.fn()}
        onPickUnder={vi.fn()}
      />
    );
    expect(screen.getByRole('button', { name: /under 47\.5 ✓/i })).toBeInTheDocument();
  });

  it('shows gameDetail string when provided', () => {
    render(<GameCard {...baseProps} gameDetail="Q3 4:32" />);
    expect(screen.getByText('Q3 4:32')).toBeInTheDocument();
  });

  it('shows "Line posted" timestamp when spreadPostedAt is provided', () => {
    render(<GameCard {...baseProps} spreadPostedAt="2023-10-19T14:00:00Z" />);
    expect(screen.getByText(/Line posted/)).toBeInTheDocument();
  });

  it('does not show "Line posted" when spreadPostedAt is absent', () => {
    render(<GameCard {...baseProps} />);
    expect(screen.queryByText(/Line posted/)).not.toBeInTheDocument();
  });
});
