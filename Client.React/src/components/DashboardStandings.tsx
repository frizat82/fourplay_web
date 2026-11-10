import { Box, Paper, Stack, Typography } from '@mui/material';
import { useMemo } from 'react';
import { useSession } from '../services/session';
import { useAuth } from '../services/auth';
import { useSportContext } from '../services/sport';
import { useLeagueLeaderboards } from '../utils/useLeagueLeaderboards';
import type { SportAdapter } from '../services/sportAdapter';

interface LeagueStanding {
  leagueId: number;
  leagueName: string;
  rank: string;
  total: number;
}

function moneyColor(total: number) {
  if (total > 0) return 'success.main';
  if (total < 0) return 'error.main';
  return 'text.primary';
}

function moneyLabel(total: number) {
  return total > 0 ? `+${total}` : `${total}`;
}

interface DashboardStandingsProps {
  adapter: SportAdapter;
}

/** "How am I doing" summary for the authenticated dashboard — per-league rank/winnings, plus a grand total. */
export default function DashboardStandings({ adapter }: DashboardStandingsProps) {
  const { availableLeagues, leaguesLoaded } = useSession();
  const { user } = useAuth();
  const { isCfb } = useSportContext();

  const leaderboards = useLeagueLeaderboards(adapter);
  const standings = useMemo(() => leaderboards?.flatMap((l): LeagueStanding[] => {
    const mine = l.leaderboard.find(r => r.userId === user?.userId);
    return mine ? [{ leagueId: l.leagueId, leagueName: l.leagueName, rank: mine.rank, total: mine.total }] : [];
  }), [leaderboards, user?.userId]);

  // Still resolving session/auth state — avoid flashing an empty state before we know whether
  // the user has leagues in this sport.
  if (!leaguesLoaded || !user?.userId) return null;

  const sportLabel = isCfb ? 'CFB' : 'NFL';

  // A user can genuinely have leagues in one sport and none in the other (e.g. NFL-only or
  // CFB-only membership) — show that explicitly instead of silently rendering nothing, so the
  // dashboard doesn't look structurally different (or broken) between subdomains for the same
  // account.
  if (availableLeagues.length === 0) {
    return (
      <Paper data-testid="dashboard-standings" elevation={3} sx={{ p: 3, borderRadius: 2 }}>
        <Typography variant="h6" fontWeight={700} sx={{ mb: 1 }}>Your Standings</Typography>
        <Typography variant="body2" color="text.secondary">
          You're not in any {sportLabel} leagues yet. Ask your commissioner for an invite.
        </Typography>
      </Paper>
    );
  }

  if (!standings) return null; // leaderboard query still in flight

  if (standings.length === 0) {
    return (
      <Paper data-testid="dashboard-standings" elevation={3} sx={{ p: 3, borderRadius: 2 }}>
        <Typography variant="h6" fontWeight={700} sx={{ mb: 1 }}>Your Standings</Typography>
        <Typography variant="body2" color="text.secondary">
          No {sportLabel} results yet this season.
        </Typography>
      </Paper>
    );
  }

  const grandTotal = standings.reduce((sum, s) => sum + s.total, 0);

  return (
    <Paper data-testid="dashboard-standings" elevation={3} sx={{ p: 3, borderRadius: 2 }}>
      <Typography variant="h6" fontWeight={700} sx={{ mb: 2 }}>Your Standings</Typography>
      <Stack spacing={1.5} divider={<Box sx={{ borderBottom: '1px solid', borderColor: 'divider' }} />}>
        {standings.map(s => (
          <Stack key={s.leagueId} direction="row" justifyContent="space-between" alignItems="center">
            <Box>
              <Typography variant="body1" fontWeight={500}>{s.leagueName}</Typography>
              <Typography variant="caption" color="text.secondary">Rank {s.rank}</Typography>
            </Box>
            <Typography variant="body1" fontWeight={700} color={moneyColor(s.total)}>
              {moneyLabel(s.total)}
            </Typography>
          </Stack>
        ))}
      </Stack>
      {standings.length > 1 && (
        <Box sx={{ mt: 2, pt: 2, borderTop: '2px solid', borderColor: 'divider' }}>
          <Stack direction="row" justifyContent="space-between">
            <Typography variant="subtitle1" fontWeight={700}>Total</Typography>
            <Typography variant="subtitle1" fontWeight={700} color={moneyColor(grandTotal)}>
              {moneyLabel(grandTotal)}
            </Typography>
          </Stack>
        </Box>
      )}
    </Paper>
  );
}
