import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi } from 'vitest';
import WeekYearSelector from '../components/WeekYearSelector';

const defaultProps = {
  season: 2025,
  week: 5,
  isPostSeason: false,
  onSeasonChange: vi.fn(),
  onWeekChange: vi.fn(),
  onSeasonTypeChange: vi.fn(),
  minSeason: 2020,
  maxSeason: 2025,
  maxRegularSeasonWeek: 18,
};

function setup(overrides?: Partial<typeof defaultProps>) {
  const props = { ...defaultProps, ...overrides };
  const user = userEvent.setup();
  const result = render(<WeekYearSelector {...props} />);
  return { user, ...result, props };
}

describe('WeekYearSelector', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // -----------------------------------------------------------------------
  // Rendering
  // -----------------------------------------------------------------------

  it('renders season and week selectors', () => {
    setup();
    expect(screen.getAllByRole('combobox')).toHaveLength(3);
    expect(screen.getByText('2025 Season')).toBeInTheDocument();
    expect(screen.getByText('Week 5')).toBeInTheDocument();
  });

  it('renders Previous and Next buttons', () => {
    setup();
    expect(screen.getByRole('button', { name: /previous/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /next/i })).toBeInTheDocument();
  });

  it('disables Previous button at minSeason week 1', () => {
    setup({ season: 2020, week: 1 });
    expect(screen.getByRole('button', { name: /previous/i })).toBeDisabled();
  });

  it('disables Next button at maxSeason last week', () => {
    setup({ season: 2025, week: 18 });
    expect(screen.getByRole('button', { name: /next/i })).toBeDisabled();
  });

  // -----------------------------------------------------------------------
  // Prev / Next navigation within same season
  // -----------------------------------------------------------------------

  it('calls onWeekChange with week - 1 when Previous clicked mid-season', async () => {
    const { user } = setup({ season: 2025, week: 5 });
    await user.click(screen.getByRole('button', { name: /previous/i }));
    expect(defaultProps.onWeekChange).toHaveBeenCalledWith(4, { isPostSeason: false });
  });

  it('calls onWeekChange with week + 1 when Next clicked mid-season', async () => {
    const { user } = setup({ season: 2025, week: 5 });
    await user.click(screen.getByRole('button', { name: /next/i }));
    expect(defaultProps.onWeekChange).toHaveBeenCalledWith(6, { isPostSeason: false });
  });

  // -----------------------------------------------------------------------
  // Year boundary — prev from week 1 goes to prior year last week
  // -----------------------------------------------------------------------

  it('calls onSeasonChange with season - 1 when Previous at week 1', async () => {
    const onSeasonChange = vi.fn();
    const onWeekChange = vi.fn();
    const { user } = setup({ season: 2025, week: 1, onSeasonChange, onWeekChange });
    await user.click(screen.getByRole('button', { name: /previous/i }));
    expect(onSeasonChange).toHaveBeenCalledWith(2024);
    // Should jump to last regular week of the previous season
    expect(onWeekChange).toHaveBeenCalledWith(18, { isPostSeason: false });
  });

  // -----------------------------------------------------------------------
  // Year boundary — next from last week goes to next year first week
  // -----------------------------------------------------------------------

  it('calls onSeasonChange with season + 1 when Next at last week and not maxSeason', async () => {
    const onSeasonChange = vi.fn();
    const onWeekChange = vi.fn();
    const { user } = setup({ season: 2024, week: 18, onSeasonChange, onWeekChange, maxSeason: 2025 });
    await user.click(screen.getByRole('button', { name: /next/i }));
    expect(onSeasonChange).toHaveBeenCalledWith(2025);
    expect(onWeekChange).toHaveBeenCalledWith(1, { isPostSeason: false });
  });

  // -----------------------------------------------------------------------
  // Postseason week names
  // -----------------------------------------------------------------------

  it('renders postseason week 1 as Wild Card', () => {
    setup({ week: 1, isPostSeason: true });
    expect(screen.getByText('Wild Card')).toBeInTheDocument();
  });

  it('renders postseason week 4 as Super Bowl', () => {
    setup({ week: 4, isPostSeason: true });
    expect(screen.getByText('Super Bowl')).toBeInTheDocument();
  });

  // -----------------------------------------------------------------------
  // Season type toggle
  // -----------------------------------------------------------------------

  it('calls onSeasonTypeChange(true) when postseason selected', async () => {
    const onSeasonTypeChange = vi.fn();
    const { user } = setup({ onSeasonTypeChange });

    // Open the season-type Select (third combobox: season, week, type)
    const combos = screen.getAllByRole('combobox');
    // The season-type select is the 3rd one
    await user.click(combos[2]);
    await user.click(screen.getByRole('option', { name: /postseason/i }));

    expect(onSeasonTypeChange).toHaveBeenCalledWith(true);
  });
});
