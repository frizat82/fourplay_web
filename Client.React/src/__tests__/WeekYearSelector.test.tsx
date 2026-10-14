import { render, screen } from '@testing-library/react';
import { ThemeProvider } from '@mui/material/styles';
import userEvent from '@testing-library/user-event';
import { vi } from 'vitest';
import WeekYearSelector from '../components/WeekYearSelector';
import { createAppTheme } from '../app/theme';
import type { SeasonWeek } from '../services/sportAdapter';

// The season's control-table rows (frizat-d47): the selector's weeks, their names and which are
// postseason all come from these — never from a hardcoded week count or name table.
const NFL_WEEKS: SeasonWeek[] = [
  ...Array.from({ length: 18 }, (_, i) => ({ week: i + 1, label: `Week ${i + 1}`, isPostSeason: false })),
  { week: 19, label: 'Wild Card Weekend', isPostSeason: true },
  { week: 20, label: 'Divisional Round', isPostSeason: true },
  { week: 21, label: 'Conference Championships', isPostSeason: true },
  { week: 22, label: 'Super Bowl LXI', isPostSeason: true },
];

const defaultProps = {
  season: 2025,
  week: 5,
  isPostSeason: false,
  onSeasonChange: vi.fn(),
  onWeekChange: vi.fn(),
  onSeasonTypeChange: vi.fn(),
  minSeason: 2020,
  maxSeason: 2025,
  weeks: NFL_WEEKS as SeasonWeek[] | undefined,
};

function setup(overrides?: Partial<typeof defaultProps> & Record<string, unknown>) {
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

  // -----------------------------------------------------------------------
  // /style-guide audit — container background/border must be theme-aware,
  // not hardcoded light-mode literals (rgba(26, 40, 71, ...) is theme.ts's
  // light-mode primary.main and renders near-invisible in dark mode)
  // -----------------------------------------------------------------------

  it('uses a dark-mode-appropriate border, not the hardcoded light literal', () => {
    const darkTheme = createAppTheme('dark');
    render(
      <ThemeProvider theme={darkTheme}>
        <WeekYearSelector {...defaultProps} />
      </ThemeProvider>,
    );
    const container = screen.getByTestId('week-year-selector-container');
    const style = getComputedStyle(container);
    expect(style.borderColor).not.toBe('rgba(26, 40, 71, 0.1)');
    expect(style.borderColor.replace(/\s/g, '')).toBe(darkTheme.palette.divider.replace(/\s/g, ''));
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

  it('names postseason weeks with their row labels', () => {
    setup({ week: 19, isPostSeason: true });
    expect(screen.getByText('Wild Card Weekend')).toBeInTheDocument();
  });

  it('names the final with its row label', () => {
    setup({ week: 22, isPostSeason: true });
    expect(screen.getByText('Super Bowl LXI')).toBeInTheDocument();
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

  // -----------------------------------------------------------------------
  // frizat-d47: the rows decide everything — any numbering, any names
  // -----------------------------------------------------------------------

  it('takes its weeks, names and postseason split from the rows (CFB: slate numbers)', async () => {
    const cfbWeeks: SeasonWeek[] = [
      ...Array.from({ length: 13 }, (_, i) => ({ week: i + 1, label: `Week ${i + 1}`, isPostSeason: false })),
      { week: 14, label: 'Conf. Championships', isPostSeason: true },
      { week: 18, label: 'CFP Championship', isPostSeason: true },
    ];
    const onWeekChange = vi.fn();
    const { user } = setup({ season: 2026, maxSeason: 2026, week: 14, isPostSeason: true, weeks: cfbWeeks, onWeekChange });

    expect(screen.getByText('Conf. Championships')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /next/i }));
    expect(onWeekChange).toHaveBeenCalledWith(18, { isPostSeason: true });
  });

  it('caps the current season at the current week, but not a past season', () => {
    setup({ season: 2025, maxSeason: 2025, week: 6, maxWeek: 6 });
    expect(screen.getByRole('button', { name: /next/i })).toBeDisabled(); // 2025 is current: week 6 is the last

    setup({ season: 2024, maxSeason: 2025, week: 6, maxWeek: 6 });
    expect(screen.getAllByRole('button', { name: /next/i })[1]).toBeEnabled(); // a past season isn't capped
  });

  it('shows the given week label while the rows are still loading', () => {
    setup({ weeks: undefined, week: 19, isPostSeason: true, weekLabel: 'Wild Card Weekend' });
    expect(screen.getByText('Wild Card Weekend')).toBeInTheDocument();
  });

  // -----------------------------------------------------------------------
  // isCurrent — "Archived" chip (frizat: makes it unmistakable at a glance
  // that a browsed-to week/season isn't the live one)
  // -----------------------------------------------------------------------

  it('shows an Archived chip when isCurrent is false', () => {
    setup({ isCurrent: false });
    expect(screen.getByText('Archived')).toBeInTheDocument();
  });

  it('shows no Archived chip when isCurrent is true', () => {
    setup({ isCurrent: true });
    expect(screen.queryByText('Archived')).not.toBeInTheDocument();
  });

  it('shows no Archived chip when isCurrent is not provided (caller doesn\'t know yet)', () => {
    setup();
    expect(screen.queryByText('Archived')).not.toBeInTheDocument();
  });
});
