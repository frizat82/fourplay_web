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

  it('shows Overed when overPickState=submitted', () => {
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
    expect(screen.getByRole('button', { name: /overed/i })).toBeInTheDocument();
  });

  it('calls onPickHome when Pick button clicked', async () => {
    const user = userEvent.setup();
    const onPickHome = vi.fn();
    render(<GameCard {...baseProps} homePickState="none" onPickHome={onPickHome} />);
    const pickBtns = screen.getAllByRole('button', { name: /^pick$/i });
    await user.click(pickBtns[1]); // home team is second pick button
    expect(onPickHome).toHaveBeenCalledOnce();
  });

  it('shows gameDetail string when provided', () => {
    render(<GameCard {...baseProps} gameDetail="Q3 4:32" />);
    expect(screen.getByText('Q3 4:32')).toBeInTheDocument();
  });
});
