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

  useEffect(() => {
    if (!targetDate) return;

    const updateCountdown = () => {
      const diff = targetDate.getTime() - new Date().getTime();
      setTimeRemaining(formatCountdown(diff));
    };

    updateCountdown();
    const interval = setInterval(updateCountdown, 1000);
    return () => clearInterval(interval);
  }, [targetDate]);

  useEffect(() => {
    if (targetDate) return;
    const handle = window.setTimeout(() => setTimeRemaining(''), 0);
    return () => window.clearTimeout(handle);
  }, [targetDate]);

  if (loading) return null;

  if (!nextSpreadJob) {
    return (
      <Box sx={{ textAlign: 'center', py: 4 }}>
        <Typography variant="h5" sx={{ fontWeight: 600 }}>
          Odds Not Posted Yet
        </Typography>
        <Typography variant="body2" color="text.secondary">
          Check back soon for the next spread release.
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
        background: 'linear-gradient(135deg, #0f172a, #1e293b)',
        color: 'common.white',
        boxShadow: '0 20px 45px rgba(15, 23, 42, 0.4)',
      }}
    >
      <Typography variant="overline" sx={{ letterSpacing: 2, opacity: 0.85 }}>
        Next Spread Reload
      </Typography>
      <Typography variant="h3" sx={{ fontWeight: 700, mt: 1 }}>
        {timeRemaining || '00:00:00'}
      </Typography>
      <Typography variant="body2" sx={{ mt: 1, opacity: 0.85 }}>
        Scheduled for {targetDate!.toLocaleString('en-US', { month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit', timeZoneName: 'short' })}
      </Typography>
    </Paper>
  );
}
