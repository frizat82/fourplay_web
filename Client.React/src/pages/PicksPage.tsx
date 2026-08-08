import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Box,
  Button,
  CircularProgress,
  Grid,
  Stack,
  Typography,
} from '@mui/material';
import CheckroomIcon from '@mui/icons-material/Checkroom';
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query';
import PageHeader from '../components/PageHeader';
import WeekYearSelector from '../components/WeekYearSelector';
import NoLeague from '../components/NoLeague';
import SpreadRelease from '../components/SpreadRelease';
import GameCard, { type PickState } from '../components/sports/GameCard';
import { useSession } from '../services/session';
import { useAuth } from '../services/auth';
import type { SportAdapter, GameView, WeekState } from '../services/sportAdapter';
import { useToast } from '../services/toast';

// Pick key: "gameId|team|pickType" — stable across NFL and CFB
function pickKey(gameId: string, team: string, pickType: string) {
  return `${gameId}|${team}|${pickType}`;
}

function gameIsLocked(game: GameView): boolean {
  if (game.gameStatus === 'final' || game.gameStatus === 'in_progress' || game.gameStatus === 'halftime') return true;
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
  const [showJerseys, setShowJerseys] = useState(false);
  // The real navigable ceiling, captured only from a current-week load — see the effect below.
  const [currentBounds, setCurrentBounds] = useState<{ maxWeek: number; maxSeason: number } | null>(null);

  const isCurrentWeek = weekState === null;
  const enabled = leaguesLoaded && !!currentLeague && !!user?.userId;

  const { data, isLoading } = useQuery({
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

  const { data: jerseyCache = {} } = useQuery({
    queryKey: [adapter.sport, 'jerseys', data?.season, data?.week],
    queryFn: () => adapter.loadJerseys!(data!.season, data!.week),
    enabled: !!adapter.loadJerseys && !!data,
    staleTime: Infinity,
    retry: false,
  });

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

  const selectPick = (gameId: string, team: string, pickType = 'Spread') => {
    if (isPicksLocked()) return;
    setUserPicks(prev => new Set(prev).add(pickKey(gameId, team, pickType)));
  };

  const unselectPick = (gameId: string, team: string, pickType = 'Spread') => {
    const key = pickKey(gameId, team, pickType);
    setUserPicks(prev => { const s = new Set(prev); s.delete(key); return s; });
  };

  const handleSubmit = async () => {
    if (!currentLeague || userPicks.size === 0) return;
    setStoringPicks(true);
    try {
      const picks = [...userPicks].map(key => {
        const [gameId, team, pickType] = key.split('|');
        return { gameId, team, pickType };
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

  // First load only — background refetches (polling, SSE-adjacent) keep the grid mounted
  if (!leaguesLoaded || isLoading) return (
    <Box><PageHeader title="Picks" />
      <Box sx={{ display: 'flex', justifyContent: 'center', mt: 8 }}><CircularProgress /></Box>
    </Box>
  );

  if (!currentLeague) return <NoLeague />;
  if (!hasOdds && isCurrentWeek) return <SpreadRelease sport={adapter.sport} />;

  const hasUnlockedGames = games.some(g => !gameIsLocked(g));
  const isPostSeasonSlate = isPostSeason;
  const showSelector = games.length > 0 || !isCurrentWeek;

  return (
    <Box>
      <PageHeader title="Picks" />

      {showSelector && (
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
          />
          {!isCurrentWeek && (
            <Box sx={{ display: 'flex', justifyContent: 'center', mt: -1, mb: 1 }}>
              <Button size="small" variant="outlined" onClick={() => setWeekState(null)}>
                Current Week
              </Button>
            </Box>
          )}
        </Box>
      )}

      <Grid container spacing={2}>
        {Object.keys(jerseyCache).length > 0 && (
          <Grid size={12} sx={{ display: 'flex', justifyContent: 'center' }}>
            <Button variant="outlined" color="info" startIcon={<CheckroomIcon />} onClick={() => setShowJerseys(p => !p)}>
              {showJerseys ? 'Show Logos' : 'Show Jerseys'}
            </Button>
          </Grid>
        )}

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
              <Button variant="contained" color="warning" disabled={userPicks.size === 0} onClick={handleClear}>
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
                homeRecord={!isPostSeasonSlate ? game.homeRecord : undefined}
                awayRecord={!isPostSeasonSlate ? game.awayRecord : undefined}
                homeJerseyUrl={showJerseys ? jerseyCache[game.homeTeam] : undefined}
                awayJerseyUrl={showJerseys ? jerseyCache[game.awayTeam] : undefined}
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
    </Box>
  );
}
