import { Box, Typography } from '@mui/material';

interface ExcludedWeekBannerProps {
  startWeek: number;
}

/**
 * Shown on Picks/Scores for a week before the league's configured StartWeek (frizat-o3x) —
 * LeaderboardService.CalculatePicks / CfbLeaderboardService.BuildLeaderboard both short-circuit
 * these weeks to WeekResult.Excluded before ever reading picks, so this is purely informational:
 * nothing submitted here affects standings (frizat-u66).
 */
export default function ExcludedWeekBanner({ startWeek }: ExcludedWeekBannerProps) {
  return (
    <Box sx={{ textAlign: 'center', py: 4 }}>
      <Typography variant="h5" sx={{ fontWeight: 600 }}>
        Week Not Scored
      </Typography>
      <Typography variant="body2" color="text.secondary">
        This league starts scoring at Week {startWeek} — picks aren&apos;t collected for this week.
      </Typography>
    </Box>
  );
}
