import { useEffect, useMemo, useState } from 'react';
import { Box, Paper, Typography } from '@mui/material';
import { getNextSpreadJob } from '../services/spreadRelease';

function formatCountdown(diffMs: number) {
  if (diffMs <= 0) return 'Available now';
  const totalSeconds = Math.floor(diffMs / 1000);
  const days = Math.floor(totalSeconds / 86400);
  const hours = Math.floor((totalSeconds % 86400) / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;
  const padded = (value: number) => value.toString().padStart(2, '0');
  const timePart = `${padded(hours)}:${padded(minutes)}:${padded(seconds)}`;
  return days > 0 ? `${days}d ${timePart}` : timePart;
}

export default function SpreadRelease() {
  const [loading, setLoading] = useState(true);
  const [nextSpreadJob, setNextSpreadJob] = useState<string | null>(null);
  const [timeRemaining, setTimeRemaining] = useState('Loading...');

  useEffect(() => {
    const load = async () => {
      setLoading(true);
      const result = await getNextSpreadJob();
      setNextSpreadJob(result ?? null);
      setLoading(false);
    };
    void load();
  }, []);

  const targetDate = useMemo(() => (nextSpreadJob ? new Date(nextSpreadJob) : null), [nextSpreadJob]);

  // Only show countdown if release is in the future and within 7 days.
  const MS_7_DAYS = 7 * 24 * 60 * 60 * 1000;
  // Compute once when targetDate changes — stable enough for "is this within 7 days"
  const showCountdown = useMemo(() => {
    if (!targetDate) return false;
    const nowMs = new Date().getTime(); // eslint-disable-line
    return targetDate.getTime() > nowMs && (targetDate.getTime() - nowMs) < MS_7_DAYS;
  }, [targetDate, MS_7_DAYS]);

  useEffect(() => {
    if (!targetDate || !showCountdown) return;
    const updateCountdown = () => {
      const diff = targetDate.getTime() - new Date().getTime();
      setTimeRemaining(formatCountdown(diff));
    };
    updateCountdown();
    const interval = setInterval(updateCountdown, 1000);
    return () => clearInterval(interval);
  }, [targetDate, showCountdown]);

  useEffect(() => {
    if (targetDate) return;
    const handle = window.setTimeout(() => setTimeRemaining(''), 0);
    return () => window.clearTimeout(handle);
  }, [targetDate]);

  if (loading) return null;

  if (!showCountdown) {
    return (
      <Box sx={{ textAlign: 'center', py: 4 }}>
        <Typography variant="h5" sx={{ fontWeight: 600 }}>
          Odds Not Posted
        </Typography>
        <Typography variant="body2" color="text.secondary">
          No spreads available for this week.
        </Typography>
      </Box>
    );
  }

  return (
    <Paper
      elevation={3}
      sx={{
        py: 4,
        px: 3,
        textAlign: 'center',
        borderRadius: 3,
        bgcolor: 'background.paper',
        border: '1px solid',
        borderColor: 'divider',
      }}
    >
      <Typography variant="overline" sx={{ letterSpacing: 2 }} color="text.secondary">
        Next Spread Reload
      </Typography>
      <Typography variant="h3" sx={{ fontWeight: 700, mt: 1 }} color="text.primary">
        {timeRemaining || '00:00:00'}
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
        Scheduled for {targetDate!.toLocaleString('en-US', { month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit', timeZoneName: 'short' })}
      </Typography>
    </Paper>
  );
}
