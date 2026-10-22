import { useEffect, useMemo, useState } from 'react';
import { Box, Paper, Typography } from '@mui/material';
import { getNextSpreadJob } from '../services/spreadRelease';
import { formatReleaseWhen, formatTimeUntil } from '../utils/releaseTime';

const HOUR_MS = 60 * 60 * 1000;

export default function SpreadRelease({ sport }: { sport: 'nfl' | 'cfb' }) {
  const [loading, setLoading] = useState(true);
  const [nextSpreadJob, setNextSpreadJob] = useState<string | null>(null);
  const [now, setNow] = useState(() => new Date());

  useEffect(() => {
    const load = async () => {
      setLoading(true);
      const result = await getNextSpreadJob(sport === 'cfb' ? 'CFB' : 'NFL');
      setNextSpreadJob(result ?? null);
      setNow(new Date()); // the first frame measures from load, not from mount
      setLoading(false);
    };
    void load();
  }, [sport]);

  const targetDate = useMemo(() => (nextSpreadJob ? new Date(nextSpreadJob) : null), [nextSpreadJob]);

  // NFL and CFB run independent spread schedules (CFB may release next week while NFL's next
  // release is 2+ weeks out) — the scheduled date is useful information regardless of how far
  // away it is, so it always renders rather than being hidden past some fixed "is this soon
  // enough" cutoff. Self-adjusting tick rate: over an hour out the card shows no seconds (see
  // formatTimeUntil), so once a minute is plenty; inside the last hour it's a live mm:ss clock.
  useEffect(() => {
    if (!targetDate) return;
    let timeoutId: ReturnType<typeof setTimeout>;
    const tick = () => {
      const current = new Date();
      setNow(current);
      const diff = targetDate.getTime() - current.getTime();
      // Never sleep past the one-hour mark, so the live clock starts right at 59:59.
      const nextDelayMs = diff > HOUR_MS ? Math.min(60_000, diff - HOUR_MS) : 1000;
      timeoutId = setTimeout(tick, nextDelayMs);
    };
    tick();
    return () => clearTimeout(timeoutId);
  }, [targetDate]);

  if (loading) return null;
  const msUntil = targetDate ? targetDate.getTime() - now.getTime() : 0;
  const lastHour = msUntil > 0 && msUntil < HOUR_MS;

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
        Spreads drop
      </Typography>
      <Typography variant="h4" component="p" sx={{ fontWeight: 700, mt: 1 }} color="text.primary">
        {formatReleaseWhen(targetDate, now)}
      </Typography>
      {/* Inside the last hour it's a live mm:ss clock — worth showing bigger. */}
      <Typography
        variant={lastHour ? 'h5' : 'body1'}
        component="p"
        color="text.secondary"
        sx={{ mt: 1, fontVariantNumeric: 'tabular-nums' }}
      >
        {formatTimeUntil(msUntil)}
      </Typography>
    </Paper>
  );
}
