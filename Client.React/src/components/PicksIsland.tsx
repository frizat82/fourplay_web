import { useMemo } from 'react';
import { Box, Chip, Paper, Stack, Typography } from '@mui/material';
import CheckIcon from '@mui/icons-material/Check';
import CloseIcon from '@mui/icons-material/Close';
import LockIcon from '@mui/icons-material/Lock';
import { useQuery } from '@tanstack/react-query';
import { useSession } from '../services/session';
import { useAuth } from '../services/auth';
import { useLeagueStartWeek } from '../utils/useLeagueStartWeek';
import { isGameLocked, isWeekExcludedFromSeason } from '../utils/gameHelpers';
import type { SportAdapter, GameView, PickView } from '../services/sportAdapter';

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
    <Paper data-testid="picks-island" elevation={3} sx={{ p: { xs: 2, sm: 3 }, borderRadius: 2 }}>
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

// Win/loss for a pick, straight off the adapter's already-computed GameView fields (homeCovers/
// awayCovers/overWins/underWins — see gameHelpers.ts's computeHomeCovers/computeAwayCovers/
// computeOverWins/computeUnderWins) — never recomputed here. null = not decided yet (or the pick's
// game wasn't found, which shouldn't happen since pick.gameId is resolved from this same response's
// games array at load time — see nflAdapter.ts's nflPickToPickView / cfbAdapter.ts's equivalent).
function pickResult(game: GameView | undefined, pick: PickView): boolean | null {
  if (!game) return null;
  if (pick.pickType === 'Over') return game.overWins ?? null;
  if (pick.pickType === 'Under') return game.underWins ?? null;
  return (pick.team === game.homeTeam ? game.homeCovers : game.awayCovers) ?? null;
}

function LeaguePicksSummary({ adapter, leagueId, leagueName, userId }: LeaguePicksSummaryProps) {
  // Same query key PicksPage uses for its own live current-week query (currentLeague, userId,
  // weekState: null) — sharing the cache, not just the shape, so the two never show conflicting
  // in-flight state for the same league.
  const { data } = useQuery({
    queryKey: [adapter.sport, 'picks', leagueId, userId, null],
    queryFn: () => adapter.loadCurrentGames(leagueId, userId),
    // Read-mostly consumer: on PicksPage the island mounts only after the page's own live query
    // (same key) has resolved, and a staleTime of 0 made it refetch that just-loaded week — the
    // whole current-week request chain ran twice on every Picks cold load. PicksPage's poll/SSE/
    // focus refetches keep the entry live; on the home page this still loads on first visit.
    staleTime: 30_000,
  });
  const startWeek = useLeagueStartWeek(leagueId, data?.season ?? new Date().getFullYear());
  const gameById = useMemo(() => new Map((data?.games ?? []).map(g => [g.id, g])), [data?.games]);

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

  const remaining = data.requiredPicks - data.userPicks.length;

  return (
    <Box>
      <Stack direction="row" justifyContent="space-between" alignItems="baseline">
        <Typography variant="body1" fontWeight={500}>{leagueName}</Typography>
        <Typography variant="caption" color="text.secondary">{weekLabel}</Typography>
      </Stack>
      {data.userPicks.length === 0 ? (
        <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
          No picks yet — {data.requiredPicks} needed
        </Typography>
      ) : (
        <>
          {/* Fixed column count (= requiredPicks, never more than 4 per pick-rules) instead of a
              wrapping flex row — a wrapping row of variable-width Chips broke onto two lines on a
              mobile viewport once labels + icons + gaps exceeded the available width. Equal-width
              grid columns guarantee every pick fits across in one row on any screen size; the
              "N more needed" count moves to its own line below since its text is far longer than a
              team code and would force a column wide enough to cramp the others. */}
          <Box
            sx={{
              display: 'grid',
              gridTemplateColumns: `repeat(${data.requiredPicks}, 1fr)`,
              gap: 0.75,
              mt: 0.75,
            }}
          >
            {data.userPicks.map(pick => (
              <PickChip key={pickLabel(pick.team, pick.pickType)} pick={pick} game={gameById.get(pick.gameId)} />
            ))}
          </Box>
          {remaining > 0 && (
            <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.75 }}>
              {remaining} more needed
            </Typography>
          )}
        </>
      )}
    </Box>
  );
}

interface PickChipProps {
  pick: PickView;
  game: GameView | undefined;
}

// Color/icon follow the style guide's pick-state semantics: success = won, error = lost, and a
// plain default chip otherwise — a lock icon distinguishes "kicked off, not decided yet" (can't
// be unselected anymore) from "still editable," without reaching for warning/amber, which the
// style guide explicitly rules out for pick-state indicators.
// width: '100%' fills its grid column exactly (rather than sizing to content, which is what let
// chips overflow a wrapping row); the label overflow guard truncates gracefully on the rare
// label that's still too wide for its column instead of ever breaking the one-row layout.
const chipSx = {
  width: '100%',
  '& .MuiChip-label': { overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' },
} as const;

function PickChip({ pick, game }: PickChipProps) {
  const label = pickLabel(pick.team, pick.pickType);
  const result = pickResult(game, pick);
  const locked = game ? isGameLocked(game) : false;

  if (result === true) return <Chip label={label} size="small" color="success" icon={<CheckIcon />} sx={chipSx} />;
  if (result === false) return <Chip label={label} size="small" color="error" icon={<CloseIcon />} sx={chipSx} />;
  return <Chip label={label} size="small" icon={locked ? <LockIcon /> : undefined} sx={chipSx} />;
}
