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

export default function SpreadRelease({ sport }: { sport: 'nfl' | 'cfb' }) {
  const [loading, setLoading] = useState(true);
  const [nextSpreadJob, setNextSpreadJob] = useState<string | null>(null);
  const [timeRemaining, setTimeRemaining] = useState('Loading...');

  useEffect(() => {
    const load = async () => {
      setLoading(true);
      const result = await getNextSpreadJob(sport === 'cfb' ? 'CFB' : 'NFL');
      setNextSpreadJob(result ?? null);
      setLoading(false);
    };
    void load();
  }, [sport]);

  const targetDate = useMemo(() => (nextSpreadJob ? new Date(nextSpreadJob) : null), [nextSpreadJob]);

  // NFL and CFB run independent spread schedules (CFB may release next week while NFL's next
  // release is 2+ weeks out) — the scheduled date is useful information regardless of how far
  // away it is, so it always renders rather than being hidden past some fixed "is this soon
  // enough" cutoff. Self-adjusting tick rate: a countdown weeks out doesn't need a per-second
  // re-render just to keep its seconds digit live — only tick every second once under an hour
  // remains, otherwise once a minute is plenty.
  useEffect(() => {
    if (!targetDate) return;
    let timeoutId: ReturnType<typeof setTimeout>;
    const tick = () => {
      const diff = targetDate.getTime() - new Date().getTime();
      setTimeRemaining(formatCountdown(diff));
      const nextDelayMs = diff > 60 * 60 * 1000 ? 60_000 : 1000;
      timeoutId = setTimeout(tick, nextDelayMs);
    };
    tick();
    return () => clearTimeout(timeoutId);
  }, [targetDate]);

  if (loading) return null;

  if (!targetDate) {
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
        {timeRemaining}
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
        Scheduled for {targetDate!.toLocaleString('en-US', { month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit', timeZoneName: 'short' })}
      </Typography>
    </Paper>
  );
}
