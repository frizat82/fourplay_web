import { useCallback, useEffect, useMemo, useState } from 'react';
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
import GameCard, { type PickState } from '../components/sports/GameCard';
import GameCardGridSkeleton from '../components/GameCardSkeleton';
import { useSession } from '../services/session';
import { useAuth } from '../services/auth';
import type { SportAdapter, GameView, PickType, WeekState } from '../services/sportAdapter';
import { useToast } from '../services/toast';
import { isGameDecided } from '../utils/gameHelpers';

// Pick key: "gameId|team|pickType" — stable across NFL and CFB
function pickKey(gameId: string, team: string, pickType: string) {
  return `${gameId}|${team}|${pickType}`;
}

function gameIsLocked(game: GameView): boolean {
  if (isGameDecided(game.gameStatus)) return true;
  return new Date(game.gameTime) <= new Date();
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
  // Pending (unsubmitted) selections — local state only, never touched by refetches
  const [userPicks, setUserPicks] = useState<Set<string>>(new Set());
  const [storingPicks, setStoringPicks] = useState(false);
  // The real navigable ceiling, captured only from a current-week load — see the effect below.
  const [currentBounds, setCurrentBounds] = useState<{ maxWeek: number; maxSeason: number } | null>(null);

  const isCurrentWeek = weekState === null;
  const enabled = leaguesLoaded && !!currentLeague && !!user?.userId;

  const { data, isLoading, isPlaceholderData, isError, refetch } = useQuery({
    queryKey: [adapter.sport, 'picks', currentLeague, user?.userId, weekState],
    queryFn: () => weekState
      ? adapter.loadHistoricalGames(currentLeague!, user!.userId, weekState)
      : adapter.loadCurrentGames(currentLeague!, user!.userId),
    enabled,
    refetchInterval: isCurrentWeek && adapter.pollIntervalMs > 0 ? adapter.pollIntervalMs : false,
    placeholderData: keepPreviousData,
  });

  // Remember the real max week/season only from a current-week load. loadHistoricalGames
  // returns maxWeek/maxSeason set to whatever week/season is being VIEWED (it has no other
  // concept of "today"), so re-deriving the selector's ceiling from `data` on every render — as
  // this used to do — collapsed the navigable range down to wherever the user last looked,
  // making it impossible to get back to the current week/season.
  useEffect(() => {
    if (isCurrentWeek && data) {
      setCurrentBounds({ maxWeek: data.maxWeek, maxSeason: data.maxSeason });
    }
  }, [isCurrentWeek, data]);

  const games = useMemo(() => data?.games ?? [], [data]);
  const hasOdds = data?.hasOdds ?? false;
  const requiredPicks = data?.requiredPicks ?? 4;
  const season = weekState?.season ?? data?.season ?? new Date().getFullYear();
  const week = weekState?.week ?? data?.week ?? 0;
  const isPostSeason = weekState?.isPostSeason ?? data?.isPostSeason ?? false;
  const maxWeek = currentBounds?.maxWeek ?? adapter.weekSelectorConfig.maxRegularSeasonWeek;
  const maxSeason = currentBounds?.maxSeason ?? new Date().getFullYear();

  const existingPicks = useMemo(
    () => new Set((data?.userPicks ?? []).map(p => pickKey(p.gameId, p.team, p.pickType))),
    [data],
  );

  // Reconcile pending selections after each (background) refresh: drop only picks
  // that are now submitted server-side or whose game has locked since selection.
  useEffect(() => {
    if (!data) return;
    const lockedIds = new Set(games.filter(gameIsLocked).map(g => g.id));
    const kept = [...userPicks].filter(k => !existingPicks.has(k) && !lockedIds.has(k.split('|')[0]));
    if (kept.length === userPicks.size) return;
    const droppedByLock = [...userPicks].some(k => !existingPicks.has(k) && lockedIds.has(k.split('|')[0]));
    setUserPicks(new Set(kept));
    if (droppedByLock) toast.push('Selection removed — game already kicked off', 'warning');
  }, [data, games, existingPicks, userPicks, toast]);

  // frizat-d2h: Show Jerseys toggle removed for now (likely permanent removal pending a
  // copyright review of the jersey images). adapter.loadJerseys and the underlying
  // /api/jersey endpoint are left intact for a future re-enable.
  const handleWeekChange = useCallback((newWeek: number, meta?: { isPostSeason?: boolean }) => {
    setWeekState({ season, week: newWeek, isPostSeason: meta?.isPostSeason ?? isPostSeason });
  }, [season, isPostSeason]);

  const handleSeasonChange = useCallback((newSeason: number) => {
    setWeekState({ season: newSeason, week, isPostSeason });
  }, [week, isPostSeason]);

  const handleSeasonTypeChange = useCallback((_ps: boolean) => {
    // WeekYearSelector.handleSeasonTypeSelect also calls onWeekChange with the last
    // available week and meta.isPostSeason — that call drives the load. No-op here.
  }, []);

  // Pick management
  const pickStateFor = (gameId: string, team: string, pickType = 'Spread'): PickState => {
    const key = pickKey(gameId, team, pickType);
    if (existingPicks.has(key)) return 'submitted';
    if (userPicks.has(key)) return 'pending';
    return 'none';
  };

  const remainingPicks = requiredPicks - userPicks.size - existingPicks.size;
  const isPicksLocked = () => remainingPicks <= 0;

  const selectPick = (gameId: string, team: string, pickType: PickType = 'Spread') => {
    if (isPicksLocked()) return;
    setUserPicks(prev => new Set(prev).add(pickKey(gameId, team, pickType)));
  };

  const unselectPick = (gameId: string, team: string, pickType: PickType = 'Spread') => {
    const key = pickKey(gameId, team, pickType);
    setUserPicks(prev => { const s = new Set(prev); s.delete(key); return s; });
  };

  const handleSubmit = async () => {
    if (!currentLeague || userPicks.size === 0) return;
    setStoringPicks(true);
    try {
      const picks = [...userPicks].map(key => {
        const [gameId, team, pickType] = key.split('|');
        return { gameId, team, pickType: pickType as PickType };
      });
      await adapter.submitPicks(currentLeague, { season, week, isPostSeason }, picks);
      toast.push(`${picks.length} Pick(s) Added`, 'success');
      setUserPicks(new Set());
      await queryClient.invalidateQueries({ queryKey: [adapter.sport, 'picks', currentLeague] });
    } catch {
      toast.push('Error Adding Picks', 'error');
    } finally {
      setStoringPicks(false);
    }
  };

  const handleClear = () => {
    // Clear only pending (unsubmitted) user picks — existing submitted picks stay
    setUserPicks(new Set());
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
  // Current week with no odds yet still needs the WeekYearSelector below rendered — otherwise a
  // visitor checking in before this week's spreads release has no way to browse to a different
  // week/season at all (frizat: previously this was a full-page early return that skipped the
  // selector entirely).
  const oddsNotReady = !hasOdds && isCurrentWeek;

  const hasUnlockedGames = games.some(g => !gameIsLocked(g));
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
          {hasUnlockedGames && (remainingPicks > 0 || userPicks.size > 0) && (
            <Grid size={12}>
              {remainingPicks > 0 && (
                <Stack spacing={1} alignItems="center">
                  <Typography variant="h6">Picks Remaining ({remainingPicks})</Typography>
                  <Typography variant="h6">Submit picks before gametime</Typography>
                </Stack>
              )}
              <Stack direction="row" spacing={2} justifyContent="space-between" sx={{ mt: 2 }}>
                <Button variant="contained" color="success" disabled={storingPicks || userPicks.size === 0} onClick={handleSubmit}>
                  {storingPicks ? 'Submitting…' : 'Submit Pick(s)'}
                </Button>
                {/* frizat: /style-guide audit — both buttons were equal-weight contained, and
                    Clear used color="warning" as a small filled button, the exact configuration
                    the style guide documents as unreadable in both modes for pick-state buttons.
                    Outlined demotes Clear to secondary, matching its rare, lower-stakes role. */}
                <Button variant="outlined" disabled={userPicks.size === 0} onClick={handleClear}>
                  Clear Selected Picks
                </Button>
              </Stack>
            </Grid>
          )}

          {!hasOdds && (
            <Grid size={12} sx={{ textAlign: 'center', py: 6 }}>
              <Typography variant="h5" fontWeight={600}>No Odds Available</Typography>
              <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>No spreads were posted for this week.</Typography>
            </Grid>
          )}

          {games.map(game => {
            const homePickState = pickStateFor(game.id, game.homeTeam);
            const awayPickState = pickStateFor(game.id, game.awayTeam);
            const overPickState = pickStateFor(game.id, game.homeTeam, 'Over');
            const underPickState = pickStateFor(game.id, game.homeTeam, 'Under');
            const locked = gameIsLocked(game);

            return (
              <Grid size={{ xs: 12, lg: 4 }} key={game.id}>
                <GameCard
                  mode="pick"
                  homeTeam={game.homeTeam}
                  awayTeam={game.awayTeam}
                  homeSpread={game.homeSpread ?? 0}
                  awaySpread={game.awaySpread ?? 0}
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
                  locked={locked}
                  onPickHome={() => homePickState !== 'none' ? unselectPick(game.id, game.homeTeam) : selectPick(game.id, game.homeTeam)}
                  onPickAway={() => awayPickState !== 'none' ? unselectPick(game.id, game.awayTeam) : selectPick(game.id, game.awayTeam)}
                  overValue={isPostSeasonSlate ? game.overUnder : undefined}
                  underValue={isPostSeasonSlate ? game.overUnder : undefined}
                  overPickState={overPickState}
                  underPickState={underPickState}
                  overUnderLocked={locked && overPickState === 'none'}
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
