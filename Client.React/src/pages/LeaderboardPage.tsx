import { useEffect, useMemo, useRef, useState } from 'react';
import { buildDescendingSeasonRange } from '../utils/seasonRange';
import { Navigate } from 'react-router-dom';
import {
  alpha,
  Box,
  Button,
  Card,
  CardContent,
  Grid,
  MenuItem,
  Select,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography,
  useTheme,
} from '@mui/material';
import IosShareIcon from '@mui/icons-material/IosShare';
import PageHeader from '../components/PageHeader';
import ShareableStandingsCard from '../components/ShareableStandingsCard';
import LeaderboardSkeleton from '../components/LeaderboardSkeleton';
import { useSession } from '../services/session';
import { useAuth } from '../services/auth';
import { getLeaderboard } from '../api/leaderboard';
import { getLeagueJuiceForSeason } from '../api/league';
import type { LeaderboardDto } from '../types/leaderboard';
import type { SportAdapter } from '../services/sportAdapter';
import { stickyColumnSx } from '../utils/tableStyles';
import { useShareLink } from '../utils/useShareLink';
import { useShareImage } from '../utils/useShareImage';
import { useLeagueMinSeason } from '../utils/useLeagueMinSeason';

interface LeaderboardPageProps {
  adapter: SportAdapter;
}

export default function LeaderboardPage({ adapter }: LeaderboardPageProps) {
  const theme = useTheme();
  const { currentLeague, availableLeagues, leaguesLoaded } = useSession();
  const { user } = useAuth();
  const { share } = useShareLink();
  const { shareImage } = useShareImage();
  const cardRef = useRef<HTMLDivElement>(null);
  const [loading, setLoading] = useState(true);
  const [leaderboard, setLeaderboard] = useState<LeaderboardDto[]>([]);
  // frizat-ugs: LeaderboardService/CfbLeaderboardService both silently return an empty list when
  // LeagueJuiceMapping has no row for the season — this tracks that distinct state so the empty
  // leaderboard renders a "not configured" message instead of the generic "no data yet" one.
  // Checked via a separate GET (getLeagueJuiceForSeason) rather than a flag on the leaderboard
  // response itself; safe from drift because both paths ultimately key off the exact same
  // repository call (GetLeagueJuiceMappingAsync(leagueId, season)) — if that lookup's semantics
  // ever change, both sides change together.
  const [juiceConfigured, setJuiceConfigured] = useState(true);
  const [season, setSeason] = useState<number | null>(null);
  // Remember the real current-season ceiling separately from `season` (which the selector
  // below can move to a past year) — otherwise re-deriving the selector's upper bound from
  // whatever season is currently being VIEWED would shrink the range after picking a past
  // year, trapping the viewer in history (same bug class PicksPage's season selector hit).
  const [maxSeason, setMaxSeason] = useState<number | null>(null);

  // Only the very first fetch FOR THE CURRENTLY SELECTED LEAGUE shows the full-page spinner — a
  // season switch within the same league keeps the page on screen and just disables the
  // selector for the moment a fetch is in flight. Scoping this per-league (not a single global
  // "ever loaded" flag) matters because AppLayout's league-selector chip can change
  // `currentLeague` while already sitting on this page: without resetting the flag here, that
  // switch would silently keep the PREVIOUS league's standings on screen (unlabeled as stale)
  // while the new league's data loaded, with no guard against an out-of-order response
  // overwriting the right one (/code-review caught this as a real, reachable regression).
  const hasLoadedOnce = useRef(false);
  const [refreshing, setRefreshing] = useState(false);

  useEffect(() => {
    if (!leaguesLoaded || !currentLeague) { setLoading(false); return; }
    hasLoadedOnce.current = false;
    void adapter.currentSeasonYear().then((seasonYear) => {
      if (!seasonYear) { setLoading(false); return; }
      setSeason(seasonYear);
      setMaxSeason(seasonYear);
    });
  }, [currentLeague, leaguesLoaded, adapter]);

  useEffect(() => {
    if (!currentLeague || season == null) return;
    // /code-review: the season selector's own race is prevented elsewhere by disabling it
    // mid-fetch, but AppLayout's separate league-switcher chip can change `currentLeague` while
    // this effect's previous run is still in flight — nothing stops league A -> B -> A happening
    // before any of those fetches resolve. `ignore` is this run's own flag, closed over fresh on
    // every dependency change; a response is only applied if this exact run is still the latest
    // one when it resolves.
    let ignore = false;
    const run = async () => {
      if (hasLoadedOnce.current) setRefreshing(true); else setLoading(true);
      try {
        const data = await getLeaderboard(currentLeague, season);
        if (ignore) return;
        const entries = data ?? [];
        // Only worth the extra round-trip when the empty-state message actually needs to
        // distinguish "not configured" from "no results yet" — most loads have real rows.
        // Resolved before either setState call so the two never render out of sync with each
        // other mid-fetch (e.g. a season switch briefly showing the wrong empty-state message).
        const configured = entries.length === 0
          ? (await getLeagueJuiceForSeason(currentLeague, season)) != null
          : true;
        if (ignore) return;
        setLeaderboard(entries);
        setJuiceConfigured(configured);
      } catch (err) {
        // /code-review: without a catch here, a rejected getLeaderboard/getLeagueJuiceForSeason
        // (e.g. a network error — http.ts's interceptor only retries on 401) left the page stuck
        // on the spinner forever, since nothing after the throw ever cleared loading/refreshing.
        if (!ignore) console.error('LeaderboardPage: failed to load leaderboard', err);
      } finally {
        if (!ignore) {
          hasLoadedOnce.current = true;
          setLoading(false);
          setRefreshing(false);
        }
      }
    };
    void run();
    return () => { ignore = true; };
  }, [currentLeague, season]);

  const leagueMinSeason = useLeagueMinSeason(currentLeague, adapter.weekSelectorConfig.minSeason);

  const seasonOptions = useMemo(() => {
    if (maxSeason == null) return [];
    return buildDescendingSeasonRange(leagueMinSeason, maxSeason);
  }, [maxSeason, leagueMinSeason]);

  const maxWeek = useMemo(() => {
    if (leaderboard.length === 0) return 0;
    return Math.max(...leaderboard.map((row) => Math.max(...row.weekResults.map((w) => w.week))));
  }, [leaderboard]);

  const getTotalColor = (total: number) => {
    if (total > 0) return 'success.main';
    if (total < 0) return 'error.main';
    return 'text.primary';
  };

  // frizat: these used literal hardcoded rgba() constants (e.g. rgba(22, 163, 74, 0.12) for
  // "Won") that don't match theme.ts's actual success/warning/error hex values at all, and stay
  // fixed at the same opacity in both light and dark mode — exactly the anti-pattern the style
  // guide warns about (a literal color at fixed opacity reads fine against one background and
  // washes out against the other). Deriving the tint from the theme's own already-mode-tuned
  // palette color (via alpha()) guarantees the background always matches the text color exactly,
  // in both themes, with one definition instead of four guessed constants. MissingGameResults
  // moved from `primary` (structural navy — very dark in light mode, reads as a near-black smudge
  // at low opacity) to `info` (this app's documented "neutral/informational" semantic), which is
  // what "results not in yet" actually means.
  const getWeekSx = (result: string) => {
    switch (result) {
      case 'Won':
        return {
          backgroundColor: alpha(theme.palette.success.main, 0.16),
          color: 'success.main',
          fontWeight: 500,
        };
      case 'MissingPicks':
        return {
          backgroundColor: alpha(theme.palette.warning.main, 0.16),
          color: 'warning.main',
          fontWeight: 500,
        };
      case 'MissingGameResults':
        return {
          backgroundColor: alpha(theme.palette.info.main, 0.16),
          color: 'info.main',
          fontWeight: 500,
        };
      default:
        return {
          backgroundColor: alpha(theme.palette.error.main, 0.16),
          color: 'error.main',
          fontWeight: 500,
        };
    }
  };

  const rowClass = (row: LeaderboardDto) => {
    if (!user?.userId) return {};
    return row.userId === user.userId
      ? { backgroundColor: 'action.hover', fontWeight: 600 }
      : {};
  };

  const myRow = useMemo(
    () => leaderboard.find((row) => row.userId === user?.userId),
    [leaderboard, user?.userId]
  );
  const leagueName = useMemo(
    () => availableLeagues.find((l) => l.leagueId === currentLeague)?.leagueName ?? 'IV League',
    [availableLeagues, currentLeague]
  );

  const [isPreparingShare, setIsPreparingShare] = useState(false);

  useEffect(() => {
    if (!isPreparingShare || !myRow) return;
    void shareImage(cardRef.current, `${myRow.userName}'s IV League Standings`, 'iv-league-standings.png').finally(() =>
      setIsPreparingShare(false)
    );
    // Only re-run when a share is actually kicked off — shareImage is a fresh function
    // identity every render and must not retrigger a capture on its own.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isPreparingShare]);

  const handleShare = () => {
    if (!myRow) {
      share('IV League Standings', window.location.href);
      return;
    }
    setIsPreparingShare(true);
  };

  if (loading) {
    return (
      <Box>
        <PageHeader title="Leaderboard" />
        <LeaderboardSkeleton />
      </Box>
    );
  }

  if (leaguesLoaded && !currentLeague) return <Navigate to="/leaguepicker" replace />;

  return (
    <Box>
      <PageHeader
        title="Leaderboard"
        action={
          <Button
            size="small"
            variant="outlined"
            startIcon={<IosShareIcon />}
            onClick={handleShare}
          >
            Share
          </Button>
        }
      />
      {season != null && seasonOptions.length > 0 && (
        // frizat: /code-review flagged that cramming this into PageHeader's title+action row
        // (already carrying the "Leaderboard" h4 + Share button) risked overflow on the app's
        // ~390px mobile primary viewport — its own row has no competing width to fight for.
        <Box sx={{ mb: 2 }}>
          <Select
            size="small"
            value={season}
            disabled={refreshing}
            onChange={(e) => setSeason(Number(e.target.value))}
            sx={{ width: { xs: '100%', sm: 'auto' } }}
          >
            {seasonOptions.map((year) => (
              <MenuItem key={year} value={year}>{year} Season</MenuItem>
            ))}
          </Select>
        </Box>
      )}
      {myRow && isPreparingShare && (
        // Mounted only while actually capturing a share image — an always-mounted hidden card
        // duplicates the user's name/rank text in the DOM, which broke a real Playwright demo
        // test (getByText('alice') matched both the standings row and this card's own text)
        // even though it's visually hidden. Positioned in normal flow (0,0) inside a zero-size
        // overflow:hidden wrapper, not at a large negative offset — WebKit's foreignObject-based
        // canvas capture (what html-to-image uses) can render blank when the source node sits
        // outside the viewport's coordinate space, which matters here since iOS Safari is this
        // app's primary audience (CLAUDE.md).
        <Box sx={{ position: 'absolute', top: 0, left: 0, width: 0, height: 0, overflow: 'hidden', pointerEvents: 'none' }} aria-hidden="true">
          <ShareableStandingsCard
            ref={cardRef}
            leagueName={leagueName}
            userName={myRow.userName}
            rank={myRow.rank}
            total={myRow.total}
          />
        </Box>
      )}
      {leaderboard.length === 0 && (
        <Typography variant="body2" color="text.secondary" sx={{ mt: 4, textAlign: 'center' }}>
          {juiceConfigured
            ? 'No leaderboard data yet for this season.'
            : 'Juice not configured for this season — ask your league owner to set it up in the League Portal.'}
        </Typography>
      )}
      {leaderboard.length > 0 && (
        <Grid container spacing={2}>
          <Grid size={12}>
          </Grid>
          <Grid size={12}>
            <Box>
              <Box sx={{ overflowX: 'auto' }}><Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Rank</TableCell>
                    <TableCell sx={stickyColumnSx}>User</TableCell>
                    <TableCell>Total</TableCell>
                    {Array.from({ length: maxWeek }).map((_, idx) => (
                      <TableCell key={idx}>{`W${maxWeek - idx}`}</TableCell>
                    ))}
                  </TableRow>
                </TableHead>
                <TableBody>
                  {leaderboard.map((row) => (
                    <TableRow key={row.userId} sx={rowClass(row)}>
                      <TableCell>{row.rank}</TableCell>
                      <TableCell sx={stickyColumnSx}>{row.userName}</TableCell>
                      <TableCell sx={{ color: getTotalColor(row.total), fontWeight: 'bold' }}>
                        {row.total}
                      </TableCell>
                      {Array.from({ length: maxWeek }).map((_, idx) => {
                        const weekIndex = maxWeek - idx - 1;
                        const weekValue = row.weekResults[weekIndex];
                        if (!weekValue) {
                          return <TableCell key={idx} />;
                        }
                        return (
                          <TableCell key={idx} sx={getWeekSx(weekValue.weekResult)}>
                            {weekValue.score}
                          </TableCell>
                        );
                      })}
                    </TableRow>
                  ))}
                </TableBody>
              </Table></Box>

              <Grid container spacing={2} justifyContent="center" sx={{ mt: 2 }}>
                <Grid size={{ xs: 12, sm: 6, md: 3 }}>
                  <Card sx={{ backgroundColor: getWeekSx('Won').backgroundColor }}>
                    <CardContent sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                      <Box
                        sx={{
                          width: 12,
                          height: 12,
                          borderRadius: 0.5,
                          backgroundColor: 'success.main',
                        }}
                      />
                      <Typography>Won</Typography>
                    </CardContent>
                  </Card>
                </Grid>
                <Grid size={{ xs: 12, sm: 6, md: 3 }}>
                  <Card sx={{ backgroundColor: getWeekSx('MissingGameResults').backgroundColor }}>
                    <CardContent sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                      <Box
                        sx={{
                          width: 12,
                          height: 12,
                          borderRadius: 0.5,
                          backgroundColor: 'info.main',
                        }}
                      />
                      <Typography>Games Incomplete</Typography>
                    </CardContent>
                  </Card>
                </Grid>
                <Grid size={{ xs: 12, sm: 6, md: 3 }}>
                  <Card sx={{ backgroundColor: getWeekSx('MissingPicks').backgroundColor }}>
                    <CardContent sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                      <Box
                        sx={{
                          width: 12,
                          height: 12,
                          borderRadius: 0.5,
                          backgroundColor: 'warning.main',
                        }}
                      />
                      <Typography>Missing Picks</Typography>
                    </CardContent>
                  </Card>
                </Grid>
                <Grid size={{ xs: 12, sm: 6, md: 3 }}>
                  <Card sx={{ backgroundColor: getWeekSx('Lost').backgroundColor }}>
                    <CardContent sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                      <Box
                        sx={{
                          width: 12,
                          height: 12,
                          borderRadius: 0.5,
                          backgroundColor: 'error.main',
                        }}
                      />
                      <Typography>Lost</Typography>
                    </CardContent>
                  </Card>
                </Grid>
              </Grid>
            </Box>
          </Grid>
        </Grid>
      )}
    </Box>
  );
}
