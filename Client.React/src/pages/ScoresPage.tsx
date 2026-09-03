import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Badge, Box, Button, Grid,
  IconButton, Paper, Stack, Typography,
} from '@mui/material';
import PersonIcon from '@mui/icons-material/Person';
import ArrowCircleUpIcon from '@mui/icons-material/ArrowCircleUp';
import ArrowCircleDownIcon from '@mui/icons-material/ArrowCircleDown';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import PageHeader from '../components/PageHeader';
import WeekYearSelector from '../components/WeekYearSelector';
import NoLeague from '../components/NoLeague';
import QueryErrorAlert from '../components/QueryErrorAlert';
import SpreadRelease from '../components/SpreadRelease';
import GameCardGridSkeleton from '../components/GameCardSkeleton';
import TeamHelmet from '../components/sports/TeamHelmet';
import UserPicksMatrix from '../components/UserPicksMatrix';
import PickDialog from '../components/PickDialog';
import FieldPosition from '../components/FieldPosition';
import { useSession } from '../services/session';
import { useAuth } from '../services/auth';
import { isGameDecided, isGameFinal, isGameLive, spreadLabel } from '../utils/gameHelpers';
import type { SportAdapter, GameView, WeekState, PickType } from '../services/sportAdapter';

// ─── Icon + color helpers (use pre-computed adapter fields) ──────────────────

function isDecided(game: GameView): boolean {
  return isGameDecided(game.gameStatus);
}

function teamWins(game: GameView, team: string, pickType: PickType): boolean | null {
  if (!isDecided(game)) return null;
  if (pickType === 'Spread') {
    if (game.homeCovers == null) return null;
    return team === game.homeTeam ? game.homeCovers : !game.homeCovers;
  }
  if (game.overWins == null) return null;
  return pickType === 'Over' ? game.overWins : !game.overWins;
}

function badgeColor(game: GameView, team: string, pickType: 'Spread' | 'Over' | 'Under'): 'success' | 'error' | 'info' | 'default' {
  if (!isDecided(game)) return 'info';
  const wins = teamWins(game, team, pickType);
  if (wins == null) return 'default';
  return wins ? 'success' : 'error';
}

// ─── Main component ──────────────────────────────────────────────────────────

interface ScoresPageProps {
  adapter: SportAdapter;
}

export default function ScoresPage({ adapter }: ScoresPageProps) {
  const { currentLeague, leaguesLoaded } = useSession();
  const { user } = useAuth();

  // null = live current week (polls in background); non-null = historical navigation
  const [weekState, setWeekState] = useState<WeekState | null>(null);
  // The real navigable ceiling and the real "current week" identity — captured only from a
  // current-week load, mirroring PicksPage's currentBounds. loadHistoricalScores returns
  // maxWeek/maxSeason/season/week set to whatever is being VIEWED, not "today" — re-deriving
  // either from the active query's data would collapse the selector's ceiling, or make selecting
  // your own current week from the dropdown look like a historical navigation.
  const [currentWeekSnapshot, setCurrentWeekSnapshot] = useState<
    (WeekState & { maxWeek: number; maxSeason: number }) | null
  >(null);
  const [isPageVisible, setIsPageVisible] = useState(true);
  const [showMatrixView, setShowMatrixView] = useState(false);
  const [showOnlyMyPicks, setShowOnlyMyPicks] = useState(false);
  const [dialogState, setDialogState] = useState<{
    open: boolean; teamAbbr: string; pickType: 'Spread' | 'Over' | 'Under';
    userNames: string[]; userNamesOver: string[]; userNamesUnder: string[];
  } | null>(null);

  const isCurrentWeek = weekState === null;
  const enabled = leaguesLoaded && !!currentLeague && !!user?.userId;

  const { data, isLoading, isPlaceholderData, isError, refetch } = useQuery({
    queryKey: [adapter.sport, 'scores', currentLeague, user?.userId, weekState],
    queryFn: () => weekState
      ? adapter.loadHistoricalScores(currentLeague!, user!.userId, weekState)
      : adapter.loadCurrentScores(currentLeague!, user!.userId),
    enabled,
    refetchInterval: query => isCurrentWeek && isPageVisible && adapter.pollIntervalMs > 0
      ? (query.state.data?.hasActiveGames ? adapter.pollIntervalMs : adapter.pollIntervalMs * 4)
      : false,
    placeholderData: keepPreviousData,
  });

  useEffect(() => {
    if (!isCurrentWeek || !data) return;
    // data gets a new reference on every poll/SSE tick (scores/clock change), which would
    // otherwise force this state — and everything derived from it below, including
    // WeekYearSelector's props — to re-render on every tick even when nothing here actually
    // changed. Bail out unless the fields this snapshot actually cares about moved.
    setCurrentWeekSnapshot(prev =>
      prev
        && prev.season === data.season && prev.week === data.week && prev.isPostSeason === data.isPostSeason
        && prev.maxWeek === data.maxWeek && prev.maxSeason === data.maxSeason
        ? prev
        : { season: data.season, week: data.week, isPostSeason: data.isPostSeason, maxWeek: data.maxWeek, maxSeason: data.maxSeason });
  }, [isCurrentWeek, data]);

  // Page visibility — pause polling for a hidden tab rather than burn cycles/battery on it.
  useEffect(() => {
    const h = () => setIsPageVisible(!document.hidden);
    document.addEventListener('visibilitychange', h);
    return () => document.removeEventListener('visibilitychange', h);
  }, []);

  // SSE — primary update mechanism when on current NFL week with active games; polling above
  // is the fallback (and the only mechanism at all for adapters with no sseUrl, e.g. CFB).
  useEffect(() => {
    if (!isCurrentWeek || !isPageVisible || !leaguesLoaded || !data?.hasActiveGames || !adapter.sseUrl) return;
    const es = new EventSource(adapter.sseUrl, { withCredentials: true });
    es.onmessage = () => void refetch();
    es.onerror = () => es.close(); // fallback polling takes over
    return () => es.close();
  }, [isCurrentWeek, isPageVisible, leaguesLoaded, data?.hasActiveGames, adapter.sseUrl, refetch]);

  const maxWeek = currentWeekSnapshot?.maxWeek ?? adapter.weekSelectorConfig.maxRegularSeasonWeek;
  const maxSeason = currentWeekSnapshot?.maxSeason ?? new Date().getFullYear();

  // Selecting the week that IS the current week (from the dropdown, not the "Current Week"
  // button) routes back to the live query instead of a one-off historical fetch — the historical
  // path has no equivalent of hasActiveGames/SSE eligibility, so it would freeze live updates.
  const routeToCurrentIfMatches = useCallback((candidate: WeekState): WeekState | null => {
    if (currentWeekSnapshot
      && candidate.season === currentWeekSnapshot.season
      && candidate.week === currentWeekSnapshot.week
      && candidate.isPostSeason === currentWeekSnapshot.isPostSeason) {
      return null;
    }
    return candidate;
  }, [currentWeekSnapshot]);

  const handleWeekChange = useCallback((week: number, meta?: { isPostSeason?: boolean }) => {
    const season = data?.season ?? new Date().getFullYear();
    const isPostSeason = meta?.isPostSeason ?? data?.isPostSeason ?? false;
    setWeekState(routeToCurrentIfMatches({ season, week, isPostSeason }));
  }, [data?.season, data?.isPostSeason, routeToCurrentIfMatches]);
  const handleSeasonChange = useCallback((season: number) => {
    const week = data?.week ?? 1;
    const isPostSeason = data?.isPostSeason ?? false;
    setWeekState(routeToCurrentIfMatches({ season, week, isPostSeason }));
  }, [data?.week, data?.isPostSeason, routeToCurrentIfMatches]);
  const handleSeasonTypeChange = useCallback((_isPostSeason: boolean) => {
    // WeekYearSelector.handleSeasonTypeSelect also calls onWeekChange with the last week —
    // don't double-load here, let handleWeekChange handle it.
  }, []);

  // Pick query helpers
  const pickCountForTeam = (gameId: string, team: string, pickType: 'Spread' | 'Over' | 'Under') =>
    (data?.allPicks ?? []).filter(p => p.gameId === gameId && p.team === team && p.pickType === pickType).length;

  const didUserPick = (gameId: string, team: string, pickType: 'Spread' | 'Over' | 'Under' = 'Spread') =>
    (data?.userPicks ?? []).some(p => p.gameId === gameId && p.team === team && p.pickType === pickType);

  const showDialog = (game: GameView, team: string, pickType: 'Spread' | 'Over' | 'Under' = 'Spread') => {
    const names = (data?.allPicks ?? []).filter(p => p.gameId === game.id && p.team === team && p.pickType === pickType).map(p => p.userName).sort();
    if (!names.length) return;
    setDialogState({
      open: true,
      teamAbbr: pickType === 'Spread' ? team : '',
      pickType,
      userNames: pickType === 'Spread' ? names : [],
      userNamesOver: pickType === 'Over' ? names : [],
      userNamesUnder: pickType === 'Under' ? names : [],
    });
  };

  const users = useMemo(() => Array.from(new Set((data?.allPicks ?? []).map(p => p.userName))), [data?.allPicks]);

  /** Build spread result map for UserPicksMatrix from GameView cover data */
  const matrixSpreads = useMemo(() => {
    const result: Record<string, { isWinner: boolean; isOverWinner: boolean; isUnderWinner: boolean; spread: number | null; over: number | null; under: number | null }> = {};
    for (const game of (data?.games ?? [])) {
      if (game.homeCovers == null) continue; // not final
      const ov = game.overWins ?? false;
      result[game.homeTeam] = { isWinner: game.homeCovers, isOverWinner: ov, isUnderWinner: !ov, spread: game.homeSpread, over: game.overUnder, under: game.overUnder };
      result[game.awayTeam] = { isWinner: !game.homeCovers, isOverWinner: ov, isUnderWinner: !ov, spread: game.awaySpread, over: game.overUnder, under: game.overUnder };
    }
    return result;
  }, [data?.games]);

  // ─── Guard states ─────────────────────────────────────────────────────────

  // isLoading covers the very first load; isPlaceholderData covers navigating to a week whose
  // data isn't cached yet — without it, keepPreviousData silently shows the PREVIOUS week's
  // stale grid with no loading indicator until the new week resolves (reported as Previous/Next
  // "freezing"). Same-key background refetches (polling, SSE) never set isPlaceholderData, so
  // those still update in place with no skeleton flash.
  //
  // /code-review: gated on `enabled` too — a *disabled* query (e.g. currentLeague just went from
  // set to null, such as the user being removed from their only league) never leaves its
  // placeholder state, since it never actually fetches. Without this, isPlaceholderData would
  // stay permanently true and this guard would never fall through to the `!currentLeague` check
  // below, trapping the page on an infinite skeleton instead of showing NoLeague.
  if (!leaguesLoaded || isLoading || (isPlaceholderData && enabled)) return (
    <Box><PageHeader title="Scores" /><GameCardGridSkeleton /></Box>
  );
  if (!currentLeague) return <NoLeague />;
  if (isError && !data) return (
    <QueryErrorAlert title="Scores" onRetry={() => void refetch()} />
  );
  if (!data) return null;

  // Current week with no odds yet still needs the WeekYearSelector below rendered — otherwise a
  // visitor checking in before this week's spreads release has no way to browse to a different
  // week/season at all (frizat: previously this was a full-page early return that skipped the
  // selector entirely).
  const oddsNotReady = !data.hasOdds && isCurrentWeek;

  const games = showOnlyMyPicks
    ? (data.games ?? []).filter(g =>
        didUserPick(g.id, g.homeTeam) || didUserPick(g.id, g.awayTeam) ||
        didUserPick(g.id, g.homeTeam, 'Over') || didUserPick(g.id, g.homeTeam, 'Under'))
    : (data.games ?? []);

  const isPostSeason = data.isPostSeason;

  return (
    <Box>
      <PageHeader title="Scores" />

      <Box sx={{ mb: 3 }}>
        <WeekYearSelector
          season={data.season}
          week={data.week}
          isPostSeason={isPostSeason}
          onSeasonChange={handleSeasonChange}
          onWeekChange={handleWeekChange}
          onSeasonTypeChange={handleSeasonTypeChange}
          {...adapter.weekSelectorConfig}
          maxRegularSeasonWeek={maxWeek}
          maxSeason={maxSeason}
          isCurrent={isCurrentWeek}
        />
        {!isCurrentWeek && (
          <Box sx={{ display: 'flex', justifyContent: 'center', mt: -1, mb: 1 }}>
            <Button size="small" variant="outlined" onClick={() => setWeekState(null)}>
              Current Week
            </Button>
          </Box>
        )}
      </Box>

      {oddsNotReady ? (
        <SpreadRelease sport={adapter.sport} />
      ) : (
        <Grid container spacing={2}>
          {/* Controls row */}
          <Grid size={12} sx={{ display: 'flex', justifyContent: 'flex-end', gap: 2 }}>
            {/* frizat: /style-guide audit — these are neutral view filters, not brand CTAs, but
                one defaulted to unstyled contained (reads as inert navy) and the other used
                contained secondary (the brand orange reserved for real CTAs like Share). Same
                matching, neutral treatment for both now. */}
            {data?.allPicks.length && data.allPicks.length > 0 && (
              <Button variant="outlined" color="info" onClick={() => setShowMatrixView(p => !p)}>
                {showMatrixView ? 'Show Standard View' : 'Show As Matrix'}
              </Button>
            )}
            {!showMatrixView && (
              <Button variant="outlined" color="info" onClick={() => setShowOnlyMyPicks(p => !p)}>
                {showOnlyMyPicks ? 'Show All Games' : 'Show Only My Picks'}
              </Button>
            )}
          </Grid>

          {/* Matrix view */}
          {showMatrixView ? (
            <Grid size={12}>
              <UserPicksMatrix
                users={users}
                picks={(data.allPicks ?? []).map(p => ({
                  id: 0, leagueId: 0, userId: p.userId, userName: p.userName,
                  team: p.team, pick: p.pickType as 'Spread' | 'Over' | 'Under',
                  nflWeek: data.week, season: data.season, dateCreated: '',
                }))}
                spreads={matrixSpreads as Record<string, import('../types/picks').SpreadCalculationResponse>}
                requiredPicks={data?.requiredPicks ?? 4}
              />
            </Grid>
          ) : (
            <>
              {!data?.hasOdds && (
                <Grid size={12} sx={{ textAlign: 'center', py: 6 }}>
                  <Typography variant="h5" fontWeight={600}>No Odds Available</Typography>
                  <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>No spreads were posted for this week.</Typography>
                </Grid>
              )}
              {data?.hasOdds && showOnlyMyPicks && games.length === 0 && (
                <Grid size={12}>
                  <Paper sx={{ p: 4, textAlign: 'center' }}>
                    <Typography color="text.secondary">You haven&apos;t made any picks for this week.</Typography>
                  </Paper>
                </Grid>
              )}

              {data?.hasOdds && games.map(game => {
                const isFinal = isGameFinal(game.gameStatus);
                const isLive = isGameLive(game.gameStatus);
                const hc = game.homeCovers ?? null;
                const ov = game.overWins ?? null;

                return (
                  <Grid size={{ xs: 12, md: 6, lg: 4 }} key={game.id}>
                    <Paper className={''} sx={{ p: 2 }}>
                      {/* Score header */}
                      <Stack direction="row" alignItems="center" justifyContent="space-between">
                        <TeamHelmet abbr={game.awayTeam} size={50} />
                        <Typography variant="h6">{isFinal || isLive ? game.awayScore : ''}</Typography>
                        <Typography variant="body2" textAlign="center">
                          {isFinal ? 'Final' : isLive ? (game.situation?.period && game.situation?.displayClock ? `Q${game.situation.period} ${game.situation.displayClock}` : 'Live') : new Date(game.gameTime).toLocaleString([], { weekday: 'short', month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' })}
                        </Typography>
                        <Typography variant="h6">{isFinal || isLive ? game.homeScore : ''}</Typography>
                        <TeamHelmet abbr={game.homeTeam} size={50} />
                      </Stack>

                      {/* Field position (NFL only — situation is a full GameSituation object) */}
                      {isLive && game.situation != null && (
                        <FieldPosition situation={game.situation} />
                      )}

                      {/* Away team pick row */}
                      <Stack direction="row" alignItems="center" sx={{ mt: 2, gap: 1.5, px: 1 }}>
                        {game.awayRank != null && (
                          <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700 }}>
                            #{game.awayRank}
                          </Typography>
                        )}
                        <Typography sx={{ minWidth: 40, fontWeight: 600 }}>{game.awayTeam}</Typography>
                        <Box sx={{ flexGrow: 1 }} />
                        <Typography variant="subtitle1" className="spread-value" sx={{ minWidth: 56, textAlign: 'right' }}>{game.awaySpread != null ? spreadLabel(game.awaySpread) : ''}</Typography>
                        <Badge
                          data-testid={`badge-${game.awayTeam}-spread`}
                          data-tone={didUserPick(game.id, game.awayTeam) ? 'info' : badgeColor(game, game.awayTeam, 'Spread')}
                          color={didUserPick(game.id, game.awayTeam) ? 'info' : badgeColor(game, game.awayTeam, 'Spread')}
                          overlap="circular"
                          badgeContent={pickCountForTeam(game.id, game.awayTeam, 'Spread')}
                          invisible={(!isFinal && !isLive) || pickCountForTeam(game.id, game.awayTeam, 'Spread') === 0}
                        >
                          {/* frizat: /code-review caught that gating `disabled` on pickCount === 0 (in
                              addition to not-decided-yet) flattens this icon's color to MUI's disabled
                              gray via the `disabled` prop, which erases the win/loss signal for a team
                              literally nobody in the league picked — the one case the removed shield
                              icon used to cover on its own, independent of picks. Disabled now tracks
                              only "not decided yet"; `invisible` above still hides the pick-count bubble
                              when nobody picked, but the button itself stays colored by outcome. */}
                          <IconButton
                            color={(isFinal || isLive) ? (hc === false ? 'success' : hc === true ? 'error' : 'inherit') : 'inherit'}
                            disabled={!isFinal && !isLive}
                            onClick={() => showDialog(game, game.awayTeam, 'Spread')}
                            size="small"
                          >
                            <PersonIcon />
                          </IconButton>
                        </Badge>
                      </Stack>

                      {/* Home team pick row */}
                      <Stack direction="row" alignItems="center" sx={{ mt: 1.5, gap: 1.5, px: 1 }}>
                        {game.homeRank != null && (
                          <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700 }}>
                            #{game.homeRank}
                          </Typography>
                        )}
                        <Typography sx={{ minWidth: 40, fontWeight: 600 }}>{game.homeTeam}</Typography>
                        <Box sx={{ flexGrow: 1 }} />
                        <Typography variant="subtitle1" className="spread-value" sx={{ minWidth: 56, textAlign: 'right' }}>{game.homeSpread != null ? spreadLabel(game.homeSpread) : ''}</Typography>
                        <Badge
                          data-testid={`badge-${game.homeTeam}-spread`}
                          data-tone={didUserPick(game.id, game.homeTeam) ? 'info' : badgeColor(game, game.homeTeam, 'Spread')}
                          color={didUserPick(game.id, game.homeTeam) ? 'info' : badgeColor(game, game.homeTeam, 'Spread')}
                          overlap="circular"
                          badgeContent={pickCountForTeam(game.id, game.homeTeam, 'Spread')}
                          invisible={(!isFinal && !isLive) || pickCountForTeam(game.id, game.homeTeam, 'Spread') === 0}
                        >
                          <IconButton
                            color={(isFinal || isLive) ? (hc === true ? 'success' : hc === false ? 'error' : 'inherit') : 'inherit'}
                            disabled={!isFinal && !isLive}
                            onClick={() => showDialog(game, game.homeTeam, 'Spread')}
                            size="small"
                          >
                            <PersonIcon />
                          </IconButton>
                        </Badge>
                      </Stack>

                      {/* Postseason O/U row */}
                      {isPostSeason && game.overUnder != null && (
                        <Stack data-testid="over-under-controls" direction="row" alignItems="center" justifyContent="space-between" sx={{ mt: 2.5, px: 1, gap: 1 }}>
                          <Badge data-testid={`badge-${game.homeTeam}-over`} color={didUserPick(game.id, game.homeTeam, 'Over') ? 'info' : badgeColor(game, game.homeTeam, 'Over')} overlap="circular"
                            badgeContent={pickCountForTeam(game.id, game.homeTeam, 'Over')}
                            invisible={(!isFinal && !isLive) || pickCountForTeam(game.id, game.homeTeam, 'Over') === 0}>
                            <IconButton size="small"
                              color={(isFinal || isLive) ? (ov ? 'success' : ov === false ? 'error' : 'inherit') : 'inherit'}
                              disabled={!isFinal && !isLive}
                              onClick={() => showDialog(game, game.homeTeam, 'Over')}>
                              <PersonIcon />
                            </IconButton>
                          </Badge>
                          {/* frizat: /style-guide audit — these were color-coded success/error on top of the
                              Badge/IconButton pairs on either side already showing the identical win/loss
                              state (same redundant-signal issue as the shield icon this page dropped
                              elsewhere). The arrows are just Over/Under labels now; the badges are the signal. */}
                          <ArrowCircleUpIcon sx={{ color: 'text.secondary', flexShrink: 0 }} />
                          <Typography variant="subtitle1" sx={{ minWidth: 36, textAlign: 'center' }}>{game.overUnder}</Typography>
                          <ArrowCircleDownIcon sx={{ color: 'text.secondary', flexShrink: 0 }} />
                          <Badge data-testid={`badge-${game.homeTeam}-under`} color={didUserPick(game.id, game.homeTeam, 'Under') ? 'info' : badgeColor(game, game.homeTeam, 'Under')} overlap="circular"
                            badgeContent={pickCountForTeam(game.id, game.homeTeam, 'Under')}
                            invisible={(!isFinal && !isLive) || pickCountForTeam(game.id, game.homeTeam, 'Under') === 0}>
                            <IconButton size="small"
                              color={(isFinal || isLive) ? (!ov ? 'success' : ov === true ? 'error' : 'inherit') : 'inherit'}
                              disabled={!isFinal && !isLive}
                              onClick={() => showDialog(game, game.homeTeam, 'Under')}>
                              <PersonIcon />
                            </IconButton>
                          </Badge>
                        </Stack>
                      )}

                      {/* ScoreTicker deferred — needs GameView-compatible refactor */}
                    </Paper>
                  </Grid>
                );
              })}
            </>
          )}
        </Grid>
      )}

      {dialogState && (
        <PickDialog
          open={dialogState.open}
          onClose={() => setDialogState(null)}
          teamAbbr={dialogState.teamAbbr}
          pickType={dialogState.pickType}
          userNames={dialogState.userNames}
          userNamesOver={dialogState.userNamesOver}
          userNamesUnder={dialogState.userNamesUnder}
        />
      )}
    </Box>
  );
}
