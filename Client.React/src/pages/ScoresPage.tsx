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
import ExcludedWeekBanner from '../components/ExcludedWeekBanner';
import GameCardGridSkeleton from '../components/GameCardSkeleton';
import TeamArt from '../components/sports/TeamArt';
import RankBadge from '../components/sports/RankBadge';
import UserPicksMatrix from '../components/UserPicksMatrix';
import PickDialog from '../components/PickDialog';
import FieldPosition from '../components/FieldPosition';
import { useSession } from '../services/session';
import { useAuth } from '../services/auth';
import { isGameDecided, isGameFinal, isGameLive, isConsistentRedZone, spreadLabel, isWeekExcludedFromSeason } from '../utils/gameHelpers';
import type { SportAdapter, GameView, WeekState, PickType } from '../services/sportAdapter';
import { sortGamesByTimeThenRank } from '../services/sportAdapter';
import { useLeagueMinSeason } from '../utils/useLeagueMinSeason';
import { useLeagueStartWeek } from '../utils/useLeagueStartWeek';
import { useCurrentWeekNav } from '../utils/useCurrentWeekNav';
import { useReconnectingEventSource } from '../utils/useReconnectingEventSource';

// ─── Icon + color helpers (use pre-computed adapter fields) ──────────────────

function isDecided(game: GameView): boolean {
  return isGameDecided(game.gameStatus);
}

function teamWins(game: GameView, team: string, pickType: PickType): boolean | null {
  if (!isDecided(game)) return null;
  if (pickType === 'Spread') {
    // Teased spreads add juice per-team (see SpreadCalculator.GetSpread) — home/away spreads
    // aren't mirror images, so the away side's result must come from its own computed value,
    // never from negating homeCovers.
    return team === game.homeTeam ? (game.homeCovers ?? null) : (game.awayCovers ?? null);
  }
  // Over/Under thresholds are juiced independently (see computeOverWins/computeUnderWins) — the
  // Under result must come from its own computed value, never from negating overWins.
  return pickType === 'Over' ? (game.overWins ?? null) : (game.underWins ?? null);
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
    // refetchOnWindowFocus is disabled globally (main.tsx) for other pages' sake, but the live
    // current-week query needs it: refetchInterval only pauses/resumes background polling — that
    // alone doesn't trigger an immediate fetch on regaining focus, so without this override the
    // page just waits out the next 5-20min poll tick after a tab switch away and back. React
    // Query's own focusManager already listens for `visibilitychange` under the hood, so this
    // reuses that built-in mechanism (scoped to just this query) instead of hand-rolling a second
    // listener. Scoped to the live week only — a historical week's data can't have changed.
    refetchOnWindowFocus: () => isCurrentWeek,
    placeholderData: keepPreviousData,
  });

  const { maxWeek: currentMaxWeek, maxSeason: currentMaxSeason, routeToCurrentIfMatches } =
    useCurrentWeekNav(isCurrentWeek, data, setWeekState);

  // Page visibility — pause polling for a hidden tab rather than burn cycles/battery on it.
  useEffect(() => {
    const h = () => setIsPageVisible(!document.hidden);
    document.addEventListener('visibilitychange', h);
    return () => document.removeEventListener('visibilitychange', h);
  }, []);

  // SSE — primary update mechanism when on current NFL/CFB week with active games; polling
  // above is the fallback. Reconnects with backoff on drop and immediately on the browser's
  // `online` event (frizat-a2u) — a dropped connection alone doesn't change any of these gating
  // conditions, so without this the stream stayed dead for the rest of the page session.
  const sseEnabled = isCurrentWeek && isPageVisible && leaguesLoaded && !!data?.hasActiveGames && !!adapter.sseUrl;
  useReconnectingEventSource(sseEnabled ? adapter.sseUrl : null, () => void refetch());

  const maxWeek = currentMaxWeek ?? adapter.weekSelectorConfig.maxRegularSeasonWeek;
  const maxSeason = currentMaxSeason ?? new Date().getFullYear();
  const minSeason = useLeagueMinSeason(currentLeague, adapter.weekSelectorConfig.minSeason);
  const startWeek = useLeagueStartWeek(currentLeague, data?.season ?? new Date().getFullYear());

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

  // /code-review: this re-derives on every render otherwise, including live-game poll/SSE ticks
  // that don't actually change data.games — memoized like matrixSpreads below.
  const sortedGames = useMemo(() => sortGamesByTimeThenRank(data?.games ?? []), [data?.games]);

  /** Build spread result map for UserPicksMatrix from GameView cover data */
  const matrixSpreads = useMemo(() => {
    const result: Record<string, { isWinner: boolean; isOverWinner: boolean; isUnderWinner: boolean; spread: number | null; over: number | null; under: number | null }> = {};
    for (const game of (data?.games ?? [])) {
      if (game.homeCovers == null || game.awayCovers == null) continue; // not final
      const ov = game.overWins ?? false;
      const uv = game.underWins ?? false;
      result[game.homeTeam] = { isWinner: game.homeCovers, isOverWinner: ov, isUnderWinner: uv, spread: game.homeSpread, over: game.overThreshold, under: game.underThreshold };
      result[game.awayTeam] = { isWinner: game.awayCovers ?? false, isOverWinner: ov, isUnderWinner: uv, spread: game.awaySpread, over: game.overThreshold, under: game.underThreshold };
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

  // No longer gated on isCurrentWeek — with navigation capped at the real current week
  // (adapter.maxWeek), an odds-less week is simply unreachable once you've moved past it, so
  // "no odds for the week I'm looking at" is the correct condition on its own (mirrors the
  // identical PicksPage.tsx fix, frizat-8y4).
  const oddsNotReady = !data.hasOdds;
  const isWeekExcluded = isWeekExcludedFromSeason(data.week, startWeek);

  const games = showOnlyMyPicks
    ? sortedGames.filter(g =>
        didUserPick(g.id, g.homeTeam) || didUserPick(g.id, g.awayTeam) ||
        didUserPick(g.id, g.homeTeam, 'Over') || didUserPick(g.id, g.homeTeam, 'Under'))
    : sortedGames;

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
          minSeason={minSeason}
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
      ) : isWeekExcluded ? (
        <ExcludedWeekBanner startWeek={startWeek} />
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
                sport={adapter.sport}
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
                const ac = game.awayCovers ?? null;
                const ov = game.overWins ?? null;
                const uv = game.underWins ?? null;
                const isCardRedZone = isLive && game.situation != null && isConsistentRedZone(game.situation);

                return (
                  <Grid size={{ xs: 12, md: 6, lg: 4 }} key={game.id}>
                    <Paper
                      data-testid={`game-card-${game.id}`}
                      data-redzone={String(isCardRedZone)}
                      sx={[{ p: 2 }, isCardRedZone && { outline: '3px solid', outlineColor: 'error.main', outlineOffset: -1 }]}
                    >
                      {/* Score header */}
                      <Stack direction="row" alignItems="center" justifyContent="space-between">
                        <TeamArt abbr={game.awayTeam} sport={adapter.sport} size={50} />
                        <Typography variant="h6">{isFinal || isLive ? game.awayScore : ''}</Typography>
                        <Typography variant="body2" textAlign="center">
                          {isFinal ? 'Final' : isLive ? (game.period && game.displayClock ? `Q${game.period} ${game.displayClock}` : 'Live') : new Date(game.gameTime).toLocaleString([], { weekday: 'short', month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' })}
                        </Typography>
                        <Typography variant="h6">{isFinal || isLive ? game.homeScore : ''}</Typography>
                        <TeamArt abbr={game.homeTeam} sport={adapter.sport} size={50} />
                      </Stack>

                      {/* Field position — shared by both NFL and CFB adapters, which populate GameSituation identically */}
                      {isLive && game.situation != null && (
                        <FieldPosition situation={game.situation} />
                      )}

                      {/* Away team pick row */}
                      <Stack direction="row" alignItems="center" sx={{ mt: 2, gap: 1.5, px: 1 }}>
                        <RankBadge rank={game.awayRank} />
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
                            color={(isFinal || isLive) ? (ac === true ? 'success' : ac === false ? 'error' : 'inherit') : 'inherit'}
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
                        <RankBadge rank={game.homeRank} />
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
                      {isPostSeason && game.overThreshold != null && game.underThreshold != null && (
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
                          {/* Over and Under are independently juiced and not necessarily the same
                              number (see gameHelpers.ts's computeOverWins/computeUnderWins) — show
                              both rather than a single shared value. */}
                          <Typography variant="subtitle1" sx={{ minWidth: 56, textAlign: 'center' }}>{game.overThreshold}/{game.underThreshold}</Typography>
                          <ArrowCircleDownIcon sx={{ color: 'text.secondary', flexShrink: 0 }} />
                          <Badge data-testid={`badge-${game.homeTeam}-under`} color={didUserPick(game.id, game.homeTeam, 'Under') ? 'info' : badgeColor(game, game.homeTeam, 'Under')} overlap="circular"
                            badgeContent={pickCountForTeam(game.id, game.homeTeam, 'Under')}
                            invisible={(!isFinal && !isLive) || pickCountForTeam(game.id, game.homeTeam, 'Under') === 0}>
                            <IconButton size="small"
                              color={(isFinal || isLive) ? (uv ? 'success' : uv === false ? 'error' : 'inherit') : 'inherit'}
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
          sport={adapter.sport}
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
