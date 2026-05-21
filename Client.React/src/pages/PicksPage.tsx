import { useCallback, useEffect, useState } from 'react';
import {
  Box,
  Button,
  CircularProgress,
  Grid,
  Stack,
  Typography,
} from '@mui/material';
import CheckroomIcon from '@mui/icons-material/Checkroom';
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

  const [loading, setLoading] = useState(true);
  const [games, setGames] = useState<GameView[]>([]);
  const [hasOdds, setHasOdds] = useState(false);
  const [requiredPicks, setRequiredPicks] = useState(4);
  const [isPostSeason, setIsPostSeason] = useState(false);
  const [week, setWeek] = useState(0);
  const [season, setSeason] = useState(new Date().getFullYear());
  const [existingPicks, setExistingPicks] = useState<Set<string>>(new Set());
  const [userPicks, setUserPicks] = useState<Set<string>>(new Set());
  const [storingPicks, setStoringPicks] = useState(false);
  const [showJerseys, setShowJerseys] = useState(false);
  const [jerseyCache, setJerseyCache] = useState<Record<string, string>>({});
  const [isCurrentWeek, setIsCurrentWeek] = useState(true);
  const [maxWeek, setMaxWeek] = useState(adapter.weekSelectorConfig.maxRegularSeasonWeek);
  const [maxSeason, setMaxSeason] = useState(adapter.weekSelectorConfig.minSeason);
  const [isPageVisible, setIsPageVisible] = useState(true);

  function applyLoaded(loaded: { games: GameView[]; hasOdds: boolean; requiredPicks: number; season: number; week: number; isPostSeason: boolean; existingPicks?: Set<string> }) {
    setGames(loaded.games);
    setHasOdds(loaded.hasOdds);
    setRequiredPicks(loaded.requiredPicks);
    setSeason(loaded.season);
    setWeek(loaded.week);
    setIsPostSeason(loaded.isPostSeason);
    if (loaded.existingPicks !== undefined) {
      setExistingPicks(loaded.existingPicks);
    }
    setUserPicks(new Set());
  }

  const reload = useCallback(async () => {
    if (!currentLeague || !user?.userId) {
      setLoading(false);
      return;
    }
    setLoading(true);
    try {
      const result = await adapter.loadCurrentGames(currentLeague, user.userId);
      const ep = new Set(result.userPicks.map(p => pickKey(p.gameId, p.team, p.pickType)));
      applyLoaded({ ...result, existingPicks: ep });
      setIsCurrentWeek(true);
      setMaxWeek(result.maxWeek);
      setMaxSeason(result.maxSeason);
      if (adapter.supportsJerseys && adapter.loadJerseys) {
        adapter.loadJerseys(result.season, result.week).then(setJerseyCache).catch(() => {});
      }
    } catch (err) {
      console.error('[PicksPage] loadCurrentGames failed:', err);
    } finally {
      setLoading(false);
    }
  }, [currentLeague, user?.userId, adapter]);

  const loadHistoricalWeek = useCallback(async (state: WeekState) => {
    if (!currentLeague || !user?.userId) return;
    setLoading(true);
    try {
      const result = await adapter.loadHistoricalGames(currentLeague, user.userId, state);
      if (!result) {
        setGames([]);
        setHasOdds(false);
        return;
      }
      const ep = new Set(result.userPicks.map(p => pickKey(p.gameId, p.team, p.pickType)));
      applyLoaded({ ...result, existingPicks: ep });
      if (adapter.supportsJerseys && adapter.loadJerseys) {
        adapter.loadJerseys(result.season, result.week).then(setJerseyCache).catch(() => {});
      }
    } finally {
      setLoading(false);
    }
  }, [currentLeague, user?.userId, adapter]);

  // Page visibility
  useEffect(() => {
    const handler = () => setIsPageVisible(!document.hidden);
    document.addEventListener('visibilitychange', handler);
    return () => document.removeEventListener('visibilitychange', handler);
  }, []);

  // Load + polling
  useEffect(() => {
    if (!isCurrentWeek || !isPageVisible || !leaguesLoaded) return;
    void reload();
    if (adapter.pollIntervalMs <= 0) return;
    const interval = setInterval(() => void reload(), adapter.pollIntervalMs);
    return () => clearInterval(interval);
  }, [reload, isCurrentWeek, isPageVisible, leaguesLoaded, adapter.pollIntervalMs]);

  const handleWeekChange = useCallback((newWeek: number, meta?: { isPostSeason?: boolean }) => {
    const ps = meta?.isPostSeason ?? isPostSeason;
    setWeek(newWeek);
    setIsPostSeason(ps);
    setIsCurrentWeek(false);
    void loadHistoricalWeek({ season, week: newWeek, isPostSeason: ps });
  }, [isPostSeason, season, loadHistoricalWeek]);

  const handleSeasonChange = useCallback((newSeason: number) => {
    setSeason(newSeason);
    setIsCurrentWeek(false);
    void loadHistoricalWeek({ season: newSeason, week, isPostSeason });
  }, [week, isPostSeason, loadHistoricalWeek]);

  const handleSeasonTypeChange = useCallback((ps: boolean) => {
    setIsPostSeason(ps);
    setIsCurrentWeek(false);
    void loadHistoricalWeek({ season, week, isPostSeason: ps });
  }, [season, week, loadHistoricalWeek]);

  // Pick management
  const isSelected = (gameId: string, team: string, pickType = 'Spread') =>
    userPicks.has(pickKey(gameId, team, pickType)) || existingPicks.has(pickKey(gameId, team, pickType));

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
      await reload();
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

  if (loading) return (
    <Box><PageHeader title="Picks" />
      <Box sx={{ display: 'flex', justifyContent: 'center', mt: 8 }}><CircularProgress /></Box>
    </Box>
  );

  if (!currentLeague) return <NoLeague />;
  if (!hasOdds && isCurrentWeek) return <SpreadRelease />;

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
              <Button size="small" variant="outlined" onClick={() => { setIsCurrentWeek(true); void reload(); }}>
                Current Week
              </Button>
            </Box>
          )}
        </Box>
      )}

      <Grid container spacing={2}>
        {adapter.supportsJerseys && Object.keys(jerseyCache).length > 0 && (
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
          const homePickState: PickState = isSelected(game.id, game.homeTeam) ? 'submitted' : 'none';
          const awayPickState: PickState = isSelected(game.id, game.awayTeam) ? 'submitted' : 'none';
          const overPickState: PickState = isSelected(game.id, game.homeTeam, 'Over') ? 'submitted' : 'none';
          const underPickState: PickState = isSelected(game.id, game.homeTeam, 'Under') ? 'submitted' : 'none';
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
