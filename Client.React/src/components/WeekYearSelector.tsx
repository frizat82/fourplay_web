import { Box, Button, MenuItem, Select, Stack } from '@mui/material';
import ChevronLeftIcon from '@mui/icons-material/ChevronLeft';
import ChevronRightIcon from '@mui/icons-material/ChevronRight';
import { useEffect, useMemo } from 'react';
import { getWeekName } from '../utils/gameHelpers';

interface WeekYearSelectorProps {
  season: number;
  week: number;
  isPostSeason: boolean;
  onSeasonChange: (season: number) => void;
  onWeekChange: (week: number, meta?: { isPostSeason?: boolean }) => void;
  onSeasonTypeChange: (isPostSeason: boolean) => void;
  regularWeekOptions?: number[];
  postSeasonWeekOptions?: number[];
  minSeason?: number;
  maxSeason?: number;
  maxRegularSeasonWeek?: number;
}

export default function WeekYearSelector({
  season,
  week,
  isPostSeason,
  onSeasonChange,
  onWeekChange,
  onSeasonTypeChange,
  regularWeekOptions,
  postSeasonWeekOptions,
  minSeason = 2020,
  maxSeason = new Date().getFullYear(),
  maxRegularSeasonWeek = 18,
}: WeekYearSelectorProps) {
  const defaultRegularWeeks = useMemo(() => Array.from({ length: maxRegularSeasonWeek }, (_, idx) => idx + 1), [
    maxRegularSeasonWeek,
  ]);
  const regularOptions = useMemo(() => {
    const base = regularWeekOptions?.length ? regularWeekOptions : defaultRegularWeeks;
    return Array.from(new Set(base)).sort((a, b) => a - b);
  }, [regularWeekOptions, defaultRegularWeeks]);
  const postSeasonOptions = useMemo(() => {
    const base = postSeasonWeekOptions?.length ? postSeasonWeekOptions : [1, 2, 3, 4];
    return Array.from(new Set(base)).sort((a, b) => a - b);
  }, [postSeasonWeekOptions]);

  const currentOptions = isPostSeason ? postSeasonOptions : regularOptions;
  const fallbackWeek = currentOptions[0] ?? 1;
  const clampedWeek = currentOptions.includes(week) ? week : fallbackWeek;

  useEffect(() => {
    if (currentOptions.length === 0) return;
    if (currentOptions.includes(week)) return;
    onWeekChange(currentOptions[0], { isPostSeason });
  }, [currentOptions, isPostSeason, onWeekChange, week]);

  const currentIndex = Math.max(0, currentOptions.indexOf(clampedWeek));

  const handlePrevWeek = () => {
    if (currentIndex > 0) {
      onWeekChange(currentOptions[currentIndex - 1], { isPostSeason });
      return;
    }
    if (season > minSeason) {
      onSeasonChange(season - 1);
      const lastRegular = regularOptions[regularOptions.length - 1] ?? maxRegularSeasonWeek;
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
    onSeasonTypeChange(value === 'postseason');
  };

  const seasonOptions = useMemo(
    () => Array.from({ length: maxSeason - minSeason + 1 }, (_, i) => minSeason + i).reverse(),
    [minSeason, maxSeason]
  );

  return (
    <Box
      sx={{
        display: 'flex',
        flexDirection: 'column',
        gap: 1.5,
        p: { xs: 1, sm: 2 },
        mb: 3,
        backgroundColor: 'rgba(26, 40, 71, 0.04)',
        borderRadius: 2,
        border: '1px solid rgba(26, 40, 71, 0.1)',
      }}
    >
      {/* Selects row */}
      <Stack direction="row" alignItems="center" justifyContent="center" sx={{ flexWrap: 'wrap', gap: 1 }}>
        <Select
          value={season}
          onChange={(e) => onSeasonChange(Number(e.target.value))}
          size="small"
          sx={{ minWidth: { xs: 90, sm: 100 } }}
        >
          {seasonOptions.map((s) => (
            <MenuItem key={s} value={s}>
              {s} Season
            </MenuItem>
          ))}
        </Select>

        <Select
          value={clampedWeek}
          onChange={(e) => onWeekChange(Number(e.target.value), { isPostSeason: isPostSeason })}
          size="small"
          sx={{ minWidth: { xs: 120, sm: 140 } }}
        >
          {currentOptions.map((w) => (
            <MenuItem key={w} value={w}>
              {getWeekName(w, isPostSeason)}
            </MenuItem>
          ))}
        </Select>

        <Select
          value={isPostSeason ? 'postseason' : 'regular'}
          onChange={(e) => handleSeasonTypeSelect(e.target.value as 'regular' | 'postseason')}
          size="small"
          sx={{ minWidth: { xs: 120, sm: 140 } }}
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
