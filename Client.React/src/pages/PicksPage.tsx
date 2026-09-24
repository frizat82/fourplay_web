import { useCallback, useMemo, useRef, useState } from 'react';
import {
  Box,
  Button,
  Grid,
  Stack,
  Typography,
} from '@mui/material';
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query';
import PageHeader from '../components/PageHeader';
import WeekYearSelector from '../components/WeekYearSelector';
import NoLeague from '../components/NoLeague';
import QueryErrorAlert from '../components/QueryErrorAlert';
import SpreadRelease from '../components/SpreadRelease';
import ExcludedWeekBanner from '../components/ExcludedWeekBanner';
import GameCard, { type PickState } from '../components/sports/GameCard';
import GameCardGridSkeleton from '../components/GameCardSkeleton';
import PicksIsland from '../components/PicksIsland';
import { useSession } from '../services/session';
import { useAuth } from '../services/auth';
import type { SportAdapter, LoadedWeek, PickType, WeekState } from '../services/sportAdapter';
import { picksQueryKey, sortGamesByTimeThenRank } from '../services/sportAdapter';
import { useToast } from '../services/toast';
import { isGameLocked, isWeekExcludedFromSeason } from '../utils/gameHelpers';
import { extractApiErrorMessage } from '../utils/apiError';
import { useLeagueMinSeason } from '../utils/useLeagueMinSeason';
import { useLeagueStartWeek } from '../utils/useLeagueStartWeek';
import { useCurrentWeekNav } from '../utils/useCurrentWeekNav';

// Pick key: "team|pickType" — never gameId. A team plays at most one game per week/slate, so
// team+pickType is already a complete, unambiguous key; gameId (ESPN's own id for NFL, a derived
// lookup for CFB) is a fragile extra join we never rely on for identity (frizat-z3a: the same
// pattern this page's sibling ScoresPage.tsx had to drop gameId from for the identical reason).
function pickKey(team: string, pickType: string) {
  return `${team}|${pickType}`;
}

interface PicksPageProps {
  adapter: SportAdapter;
}

export default function PicksPage({ adapter }: PicksPageProps) {
  const { currentLeague, leaguesLoaded } = useSession();
  const { user } = useAuth();
  const toast = useToast();
  const queryClient = useQueryClient();

  // null = live current week (polls in background); non-null = historical navigation
  const [weekState, setWeekState] = useState<WeekState | null>(null);
  // Pick keys with a select/unselect request currently in flight — guards against a second
  // click on the same pick firing a duplicate request while the first is still outstanding.
  // Not "unsubmitted" state: every pick is written to the server immediately on click.
  // A ref, not useState: two synchronous clicks (before React re-renders) would otherwise both
  // read the same stale, pre-update set and the guard would never actually catch anything.
  const inFlightKeysRef = useRef<Set<string>>(new Set());

  const isCurrentWeek = weekState === null;
  const enabled = leaguesLoaded && !!currentLeague && !!user?.userId;
  const queryKey = useMemo(
    () => picksQueryKey(adapter.sport, currentLeague, user?.userId, weekState),
    [adapter.sport, currentLeague, user?.userId, weekState],
  );

  const { data, isLoading, isPlaceholderData, isError, refetch } = useQuery({
    queryKey,
    queryFn: () => weekState
      ? adapter.loadHistoricalGames(currentLeague!, user!.userId, weekState)
      : adapter.loadCurrentGames(currentLeague!, user!.userId),
    enabled,
    refetchInterval: isCurrentWeek && adapter.pollIntervalMs > 0 ? adapter.pollIntervalMs : false,
    // refetchOnWindowFocus is disabled globally (main.tsx) for other pages' sake, but the live
    // current-week query needs it: refetchInterval alone doesn't trigger an immediate fetch on
    // regaining focus, so without this override the page would wait out the next 5-20min poll
    // tick after a tab switch away and back (same fix as ScoresPage.tsx). Scoped to the live
    // week only — a historical week's data can't have changed.
    refetchOnWindowFocus: () => isCurrentWeek,
    placeholderData: keepPreviousData,
  });

  const { maxWeek: currentMaxWeek, maxSeason: currentMaxSeason, routeToCurrentIfMatches } =
    useCurrentWeekNav(isCurrentWeek, data, setWeekState);

  const games = useMemo(() => sortGamesByTimeThenRank(data?.games ?? []), [data]);
  const gameById = useMemo(() => new Map(games.map(g => [g.id, g])), [games]);
  const hasOdds = data?.hasOdds ?? false;
  const requiredPicks = data?.requiredPicks ?? 4;
  const season = weekState?.season ?? data?.season ?? new Date().getFullYear();
  const week = weekState?.week ?? data?.week ?? 0;
  const isPostSeason = weekState?.isPostSeason ?? data?.isPostSeason ?? false;
  const maxWeek = currentMaxWeek ?? adapter.weekSelectorConfig.maxRegularSeasonWeek;
  const maxSeason = currentMaxSeason ?? new Date().getFullYear();
  const minSeason = useLeagueMinSeason(currentLeague, adapter.weekSelectorConfig.minSeason);
  const startWeek = useLeagueStartWeek(currentLeague, season);

  // Every pick — whether it's always been there or was optimistically added/removed by this
  // session's own click just now — lives in data.userPicks. There is no separate "pending"
  // bucket anymore: a background refetch (poll/SSE) simply replaces this with server truth,
  // which is also what corrects an optimistic update if it ever drifts.
  const existingPicks = useMemo(
    () => new Set((data?.userPicks ?? []).map(p => pickKey(p.team, p.pickType))),
    [data],
  );

  // frizat-d2h: Show Jerseys toggle removed for now (likely permanent removal pending a
  // copyright review of the jersey images). adapter.loadJerseys and the underlying
  // /api/jersey endpoint are left intact for a future re-enable.
  const handleWeekChange = useCallback((newWeek: number, meta?: { isPostSeason?: boolean }) => {
    setWeekState(routeToCurrentIfMatches({ season, week: newWeek, isPostSeason: meta?.isPostSeason ?? isPostSeason }));
  }, [season, isPostSeason, routeToCurrentIfMatches]);

  const handleSeasonChange = useCallback((newSeason: number) => {
    setWeekState(routeToCurrentIfMatches({ season: newSeason, week, isPostSeason }));
  }, [week, isPostSeason, routeToCurrentIfMatches]);

  const handleSeasonTypeChange = useCallback((_ps: boolean) => {
    // WeekYearSelector.handleSeasonTypeSelect also calls onWeekChange with the last
    // available week and meta.isPostSeason — that call drives the load. No-op here.
  }, []);

  // Pick management — 'submitted' means locked/uneditable (the game has kicked off); 'pending'
  // is repurposed here from its original "not yet sent to the server" meaning to "picked and
  // still editable" — GameCard already renders that state as a clickable "Picked" button, which
  // is exactly what an unlocked pick needs. Every pick reflected here IS persisted server-side
  // the moment its own request resolves; there is no unsubmitted state anymore.
  const pickStateFor = (gameId: string, team: string, pickType = 'Spread'): PickState => {
    const key = pickKey(team, pickType);
    if (!existingPicks.has(key)) return 'none';
    const game = gameById.get(gameId);
    return game && isGameLocked(game) ? 'submitted' : 'pending';
  };

  const remainingPicks = requiredPicks - existingPicks.size;
  const picksAtCap = remainingPicks <= 0;

  // Writes the click's effect into the query cache immediately (so the button's state flips with
  // no round-trip delay), fires the real request, and rolls back to the pre-click snapshot if the
  // server rejects it (cap exceeded, game kicked off since page load, network error) — surfacing
  // the actual reason via the existing extractApiErrorMessage helper. inFlightKeysRef guards
  // against a second click on the same pick firing a duplicate request before the first settles.
  const applyPickChange = async (
    key: string,
    mutatePicks: (prevPicks: LoadedWeek['userPicks']) => LoadedWeek['userPicks'],
    action: () => Promise<void>,
    fallbackMessage: string,
  ) => {
    if (inFlightKeysRef.current.has(key) || !currentLeague) return;
    inFlightKeysRef.current.add(key);
    const previous = queryClient.getQueryData<LoadedWeek | null>(queryKey);
    queryClient.setQueryData<LoadedWeek | null | undefined>(queryKey, old =>
      old ? { ...old, userPicks: mutatePicks(old.userPicks) } : old);
    try {
      await action();
      // frizat-a60: ScoresPage's matrix view reads a separate ['scores', ...] query — this
      // mutation only ever updates PicksPage's own ['picks', ...] cache entry above, so without
      // this, a pick made here stayed invisible on the Scores matrix (which shows the current
      // user's own pick immediately, unlike other users' — see revealPicksForStartedGames) until
      // something else happened to refetch it. Partial key match invalidates every weekState for
      // this league/user, not just the current one.
      void queryClient.invalidateQueries({ queryKey: [adapter.sport, 'scores', currentLeague, user!.userId] });
    } catch (err) {
      queryClient.setQueryData(queryKey, previous);
      toast.push(extractApiErrorMessage(err, fallbackMessage), 'error');
    } finally {
      inFlightKeysRef.current.delete(key);
    }
  };

  const selectPick = (gameId: string, team: string, pickType: PickType = 'Spread') => {
    if (picksAtCap || !currentLeague || !user) return;
    const key = pickKey(team, pickType);
    void applyPickChange(
      key,
      picks => [...picks, { gameId, team, pickType, userId: user.userId, userName: user.name ?? '' }],
      () => adapter.submitPicks(currentLeague, { season, week, isPostSeason }, [{ gameId, team, pickType }]),
      'Error adding pick',
    );
  };

  const unselectPick = (gameId: string, team: string, pickType: PickType = 'Spread') => {
    if (!currentLeague) return;
    const key = pickKey(team, pickType);
    void applyPickChange(
      key,
      picks => picks.filter(p => !(p.team === team && p.pickType === pickType)),
      () => adapter.removePick(currentLeague, { season, week, isPostSeason }, { gameId, team, pickType }),
      'Error removing pick',
    );
  };

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
    <Box><PageHeader title="Picks" /><GameCardGridSkeleton /></Box>
  );

  if (!currentLeague) return <NoLeague />;
  if (isError && !data) return (
    <QueryErrorAlert title="Picks" onRetry={() => void refetch()} />
  );
  // No longer gated on isCurrentWeek — with navigation capped at the real current week
  // (adapter.maxWeek), an odds-less week is simply unreachable once you've moved past it, so
  // "no odds for the week I'm looking at" is the correct condition on its own. Previously this
  // also had to be true for the *current* week specifically, or a stale isCurrentWeek flag
  // left a spread-less future week's pick buttons fully clickable (frizat-8y4).
  const oddsNotReady = !hasOdds;
  const isWeekExcluded = isWeekExcludedFromSeason(week, startWeek);

  const hasUnlockedGames = games.some(g => !isGameLocked(g));
  const isPostSeasonSlate = isPostSeason;

  return (
    <Box>
      <PageHeader title="Picks" />

      <Box sx={{ mb: 3 }}>
        <WeekYearSelector
          season={season}
          week={week}
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

      {/* frizat: PicksIsland was designed as a shared "quick view across all your leagues"
          widget for both the home page and this page — it only ever reflects the CURRENT week's
          picks (same as the home page), so it's only shown while browsing the current week, not
          a historical one, where it would show the wrong week's data next to the right one. */}
      {isCurrentWeek && <Box sx={{ mb: 3 }}><PicksIsland adapter={adapter} /></Box>}

      {oddsNotReady ? (
        <SpreadRelease sport={adapter.sport} />
      ) : isWeekExcluded ? (
        <ExcludedWeekBanner startWeek={startWeek} />
      ) : (
        <Grid container spacing={2}>
          {hasUnlockedGames && remainingPicks > 0 && (
            <Grid size={12}>
              <Stack spacing={0.5} alignItems="center">
                <Typography variant="h6">Picks Remaining ({remainingPicks})</Typography>
                <Typography variant="body2" color="text.secondary">
                  Tap a team to pick it — tap again to change your mind before kickoff
                </Typography>
              </Stack>
            </Grid>
          )}

          {games.map(game => {
            const homePickState = pickStateFor(game.id, game.homeTeam);
            const awayPickState = pickStateFor(game.id, game.awayTeam);
            const overPickState = pickStateFor(game.id, game.homeTeam, 'Over');
            const underPickState = pickStateFor(game.id, game.homeTeam, 'Under');
            const locked = isGameLocked(game);
            // A game can be individually unlocked (hasn't kicked off yet) while the league-wide
            // pick cap is already full — e.g. Monday Night Football sitting there Sunday night
            // once all 4 picks are locked in from earlier games. `locked` alone only reflects this
            // game's own kickoff time, so an at-cap game's "Pick" buttons rendered enabled and
            // clicking silently no-opped (selectPick's own picksAtCap bail-out) instead of
            // being visibly disabled. Only gates the *unpicked* ("Pick") buttons — an already-picked
            // team must stay clickable to unselect even while at the cap.
            const disableUnpicked = locked || picksAtCap;

            return (
              <Grid size={{ xs: 12, lg: 4 }} key={game.id}>
                <GameCard
                  mode="pick"
                  sport={adapter.sport}
                  homeTeam={game.homeTeam}
                  awayTeam={game.awayTeam}
                  homeSpread={game.homeSpread}
                  awaySpread={game.awaySpread}
                  gameTime={game.gameTime}
                  gameStatus={game.gameStatus ?? undefined}
                  spreadPostedAt={game.spreadPostedAt}
                  homeRecord={!isPostSeasonSlate ? game.homeRecord : undefined}
                  awayRecord={!isPostSeasonSlate ? game.awayRecord : undefined}
                  homeRank={game.homeRank}
                  awayRank={game.awayRank}
                  weatherDisplayValue={game.weather?.displayValue}
                  weatherConditionId={game.weather?.conditionId}
                  weatherTemperatureF={game.weather?.temperatureF}
                  isPostSeason={isPostSeasonSlate}
                  homePickState={homePickState}
                  awayPickState={awayPickState}
                  locked={disableUnpicked}
                  onPickHome={() => homePickState !== 'none' ? unselectPick(game.id, game.homeTeam) : selectPick(game.id, game.homeTeam)}
                  onPickAway={() => awayPickState !== 'none' ? unselectPick(game.id, game.awayTeam) : selectPick(game.id, game.awayTeam)}
                  overValue={isPostSeasonSlate ? game.overThreshold : undefined}
                  underValue={isPostSeasonSlate ? game.underThreshold : undefined}
                  overPickState={overPickState}
                  underPickState={underPickState}
                  overUnderLocked={disableUnpicked && overPickState === 'none'}
                  onPickOver={() => overPickState !== 'none' ? unselectPick(game.id, game.homeTeam, 'Over') : selectPick(game.id, game.homeTeam, 'Over')}
                  onPickUnder={() => underPickState !== 'none' ? unselectPick(game.id, game.homeTeam, 'Under') : selectPick(game.id, game.homeTeam, 'Under')}
                />
              </Grid>
            );
          })}
        </Grid>
      )}
    </Box>
  );
}
