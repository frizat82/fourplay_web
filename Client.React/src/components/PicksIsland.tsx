import { Box, Chip, Paper, Stack, Typography } from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import { useSession } from '../services/session';
import { useAuth } from '../services/auth';
import { useLeagueStartWeek } from '../utils/useLeagueStartWeek';
import { isWeekExcludedFromSeason } from '../utils/gameHelpers';
import type { SportAdapter } from '../services/sportAdapter';

interface PicksIslandProps {
  adapter: SportAdapter;
}

/**
 * "Quick view" of the current week's picks — no spreads, just which teams are picked and how
 * many more are needed. Shared by PicksPage (implicitly, via the identical query key below) and
 * the home page: both read the exact same React Query cache entry per league, so opening this on
 * the home page costs no extra request once PicksPage has already loaded, and vice versa.
 */
export default function PicksIsland({ adapter }: PicksIslandProps) {
  const { availableLeagues, leaguesLoaded } = useSession();
  const { user } = useAuth();

  if (!leaguesLoaded || !user?.userId || availableLeagues.length === 0) return null;

  return (
    <Paper data-testid="picks-island" elevation={3} sx={{ p: 3, borderRadius: 2 }}>
      <Typography variant="h6" fontWeight={700} sx={{ mb: 2 }}>Your Picks</Typography>
      <Stack spacing={2} divider={<Box sx={{ borderBottom: '1px solid', borderColor: 'divider' }} />}>
        {availableLeagues.map(league => (
          <LeaguePicksSummary
            key={league.leagueId}
            adapter={adapter}
            leagueId={league.leagueId}
            leagueName={league.leagueName ?? 'League'}
            userId={user.userId}
          />
        ))}
      </Stack>
    </Paper>
  );
}

interface LeaguePicksSummaryProps {
  adapter: SportAdapter;
  leagueId: number;
  leagueName: string;
  userId: string;
}

// Team label for the quick view — Over/Under picks reference the home team (gameHelpers/cfbAdapter
// convention), so they need a suffix to read as distinct picks rather than a duplicate team chip.
function pickLabel(team: string, pickType: string): string {
  if (pickType === 'Over') return `${team} O`;
  if (pickType === 'Under') return `${team} U`;
  return team;
}

function LeaguePicksSummary({ adapter, leagueId, leagueName, userId }: LeaguePicksSummaryProps) {
  // Same query key PicksPage uses for its own live current-week query (currentLeague, userId,
  // weekState: null) — sharing the cache, not just the shape, so the two never show conflicting
  // in-flight state for the same league.
  const { data } = useQuery({
    queryKey: [adapter.sport, 'picks', leagueId, userId, null],
    queryFn: () => adapter.loadCurrentGames(leagueId, userId),
  });
  const startWeek = useLeagueStartWeek(leagueId, data?.season ?? new Date().getFullYear());

  if (!data) {
    return (
      <Box>
        <Typography variant="body1" fontWeight={500}>{leagueName}</Typography>
        <Typography variant="body2" color="text.secondary">Loading…</Typography>
      </Box>
    );
  }

  const weekLabel = adapter.weekSelectorConfig.weekLabelFn?.(data.week, data.isPostSeason) ?? `Week ${data.week}`;

  if (!data.hasOdds) {
    return (
      <Box>
        <Typography variant="body1" fontWeight={500}>{leagueName}</Typography>
        <Typography variant="body2" color="text.secondary">{weekLabel} — spreads not released yet</Typography>
      </Box>
    );
  }

  if (isWeekExcludedFromSeason(data.week, startWeek)) {
    return (
      <Box>
        <Typography variant="body1" fontWeight={500}>{leagueName}</Typography>
        <Typography variant="body2" color="text.secondary">Picks open at Week {startWeek}</Typography>
      </Box>
    );
  }

  const pickedTeams = data.userPicks.map(p => pickLabel(p.team, p.pickType));
  const remaining = data.requiredPicks - pickedTeams.length;

  return (
    <Box>
      <Stack direction="row" justifyContent="space-between" alignItems="baseline">
        <Typography variant="body1" fontWeight={500}>{leagueName}</Typography>
        <Typography variant="caption" color="text.secondary">{weekLabel}</Typography>
      </Stack>
      {pickedTeams.length === 0 ? (
        <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
          No picks yet — {data.requiredPicks} needed
        </Typography>
      ) : (
        <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap" sx={{ mt: 0.75 }}>
          {pickedTeams.map(label => (
            <Chip key={label} label={label} size="small" color="secondary" />
          ))}
          {remaining > 0 && (
            <Chip label={`${remaining} more needed`} size="small" variant="outlined" />
          )}
        </Stack>
      )}
    </Box>
  );
}
