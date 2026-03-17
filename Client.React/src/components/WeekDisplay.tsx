import { Typography } from '@mui/material';
import { getWeekName } from '../utils/gameHelpers';

interface WeekDisplayProps {
  week: number;
  isPostSeason: boolean;
}

export default function WeekDisplay({ week, isPostSeason }: WeekDisplayProps) {
  return (
    <Typography align="center" className="scoreboard-title">
      {getWeekName(week, isPostSeason)}
    </Typography>
  );
}
