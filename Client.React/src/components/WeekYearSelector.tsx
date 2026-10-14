import { Box, Button, Chip, MenuItem, Select, Stack } from '@mui/material';
import ChevronLeftIcon from '@mui/icons-material/ChevronLeft';
import ChevronRightIcon from '@mui/icons-material/ChevronRight';
import { useEffect, useMemo } from 'react';
import { buildDescendingSeasonRange } from '../utils/seasonRange';
import type { SeasonWeek } from '../services/sportAdapter';

interface WeekYearSelectorProps {
  season: number;
  week: number;
  isPostSeason: boolean;
  onSeasonChange: (season: number) => void;
  onWeekChange: (week: number, meta?: { isPostSeason?: boolean }) => void;
  onSeasonTypeChange: (isPostSeason: boolean) => void;
  // The season's weeks as its control-table rows define them (frizat-d47) — which weeks exist,
  // which are postseason, and what each is called. undefined while they load.
  weeks: SeasonWeek[] | undefined;
  // The current week (the live one): within the current season (maxSeason) navigation stops
  // there — later regular-season weeks have no spreads yet. Postseason stays browsable.
  maxWeek?: number;
  // The shown week's name, used until the rows load.
  weekLabel?: string;
  minSeason?: number;
  maxSeason?: number;
  // Whether the displayed season/week IS the live current one — false shows an "Archived" chip so
  // it's unmistakable at a glance that this isn't the active week (undefined = caller doesn't know
  // yet, e.g. still loading, so no chip either way).
  isCurrent?: boolean;
}

export default function WeekYearSelector({
  season,
  week,
  isPostSeason,
  onSeasonChange,
  onWeekChange,
  onSeasonTypeChange,
  weeks,
  maxWeek,
  weekLabel,
  minSeason = 2020,
  maxSeason = new Date().getFullYear(),
  isCurrent,
}: WeekYearSelectorProps) {
  const weekNumbers = (rows: SeasonWeek[]) => rows.map((w) => w.week).sort((a, b) => a - b);
  // Every regular-season week of the season — where Previous lands when crossing into it.
  const allRegular = useMemo(() => weekNumbers((weeks ?? []).filter((w) => !w.isPostSeason)), [weeks]);
  const isCappedSeason = maxWeek !== undefined && season === maxSeason;
  const regularOptions = useMemo(
    () => (isCappedSeason ? allRegular.filter((w) => w <= maxWeek!) : allRegular),
    [allRegular, isCappedSeason, maxWeek],
  );
  const postSeasonOptions = useMemo(() => weekNumbers((weeks ?? []).filter((w) => w.isPostSeason)), [weeks]);
  const labels = useMemo(() => new Map((weeks ?? []).map((w) => [w.week, w.label])), [weeks]);

  const currentOptions = isPostSeason ? postSeasonOptions : regularOptions;
  const hasOptions = currentOptions.length > 0;
  const fallbackWeek = currentOptions[0] ?? 1;
  const clampedWeek = currentOptions.includes(week) ? week : fallbackWeek;

  useEffect(() => {
    if (currentOptions.length === 0) return;
    if (currentOptions.includes(week)) return;
    // Default to the LAST available week (most recent data), not the first
    onWeekChange(currentOptions[currentOptions.length - 1], { isPostSeason });
  }, [currentOptions, isPostSeason, onWeekChange, week]);

  const currentIndex = Math.max(0, currentOptions.indexOf(clampedWeek));

  const handlePrevWeek = () => {
    if (currentIndex > 0) {
      onWeekChange(currentOptions[currentIndex - 1], { isPostSeason });
      return;
    }
    if (season > minSeason) {
      onSeasonChange(season - 1);
      // A past season is never capped: land on its last regular week (once its own rows load,
      // the selector re-clamps if that season is shaped differently).
      const lastRegular = allRegular[allRegular.length - 1] ?? week;
      onWeekChange(lastRegular, { isPostSeason: false });
    }
  };

  const handleNextWeek = () => {
    if (currentIndex < currentOptions.length - 1) {
      onWeekChange(currentOptions[currentIndex + 1], { isPostSeason });
      return;
    }
    if (season < maxSeason) {
      onSeasonChange(season + 1);
      const firstRegular = regularOptions[0] ?? 1;
      onWeekChange(firstRegular, { isPostSeason: false });
    }
  };

  const handleSeasonTypeSelect = (value: 'regular' | 'postseason') => {
    const newIsPostSeason = value === 'postseason';
    onSeasonTypeChange(newIsPostSeason);
    // Explicitly jump to the last available week so we always land on the most recent data
    const opts = newIsPostSeason ? postSeasonOptions : regularOptions;
    if (opts.length > 0) {
      onWeekChange(opts[opts.length - 1], { isPostSeason: newIsPostSeason });
    }
  };

  const seasonOptions = useMemo(
    () => buildDescendingSeasonRange(minSeason, maxSeason),
    [minSeason, maxSeason]
  );

  return (
    <Box
      data-testid="week-year-selector-container"
      sx={{
        display: 'flex',
        flexDirection: 'column',
        gap: 1.5,
        p: { xs: 1, sm: 2 },
        mb: 3,
        bgcolor: 'action.hover',
        borderRadius: 2,
        border: '1px solid',
        borderColor: 'divider',
      }}
    >
      {isCurrent === false && (
        <Box sx={{ display: 'flex', justifyContent: 'center' }}>
          <Chip label="Archived" size="small" color="warning" variant="outlined" />
        </Box>
      )}

      {/* Selects row — frizat: flexWrap alone made mobile layout depend on content width: a short
          label (e.g. "Super Bowl") left enough room for all three selects to share one row, a
          long one ("Conference Championship") pushed to a stacked layout — same page, two
          different arrangements depending on which week happened to be selected. Below `sm`,
          always stack one-per-row (direction/width driven by breakpoint, not by wrap) so the
          layout is consistent regardless of label length. */}
      <Stack
        direction={{ xs: 'column', sm: 'row' }}
        alignItems="center"
        justifyContent="center"
        sx={{ gap: 1 }}
      >
        <Select
          value={season}
          onChange={(e) => onSeasonChange(Number(e.target.value))}
          size="small"
          sx={{ width: { xs: '100%', sm: 150 } }}
        >
          {seasonOptions.map((s) => (
            <MenuItem key={s} value={s}>
              {s} Season
            </MenuItem>
          ))}
        </Select>

        <Select
          value={hasOptions ? clampedWeek : week}
          onChange={(e) => onWeekChange(Number(e.target.value), { isPostSeason: isPostSeason })}
          size="small"
          // frizat: minWidth let MUI auto-size the box to whichever label happened to be
          // selected — "Conference Championship" (the longest label across both sports) is
          // ~117px wider than "Wild Card"/"Week 1", so the whole select row (centered via
          // justifyContent) visibly shifted left/right depending which week was picked. A fixed
          // width on sm+ keeps the row's total width constant regardless of week, so the centered
          // group never moves; full-width on xs since mobile always stacks one-per-row now.
          sx={{ width: { xs: '100%', sm: 260 } }}
        >
          {hasOptions
            ? currentOptions.map((w) => (
                <MenuItem key={w} value={w}>
                  {labels.get(w)}
                </MenuItem>
              ))
            // Rows still loading (or none for this season): show just the current week, by the
            // name the page already has.
            : <MenuItem value={week}>{weekLabel ?? ''}</MenuItem>}
        </Select>

        <Select
          value={isPostSeason ? 'postseason' : 'regular'}
          onChange={(e) => handleSeasonTypeSelect(e.target.value as 'regular' | 'postseason')}
          size="small"
          sx={{ width: { xs: '100%', sm: 170 } }}
        >
          <MenuItem value="regular">Regular Season</MenuItem>
          <MenuItem value="postseason">Postseason</MenuItem>
        </Select>
      </Stack>

      {/* Prev / Next row */}
      <Stack direction="row" spacing={2} alignItems="center" justifyContent="space-between">
        <Button
          variant="outlined"
          size="small"
          startIcon={<ChevronLeftIcon />}
          onClick={handlePrevWeek}
          disabled={season === minSeason && (currentOptions.length === 0 || currentIndex === 0)}
        >
          Previous
        </Button>
        <Button
          variant="outlined"
          size="small"
          endIcon={<ChevronRightIcon />}
          onClick={handleNextWeek}
          disabled={season === maxSeason && (currentOptions.length === 0 || currentIndex === currentOptions.length - 1)}
        >
          Next
        </Button>
      </Stack>

    </Box>
  );
}
