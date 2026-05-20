import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Badge, Box, Button, CircularProgress, Grid,
  IconButton, Paper, Stack, Typography,
} from '@mui/material';
import PersonIcon from '@mui/icons-material/Person';
import GppGoodIcon from '@mui/icons-material/GppGood';
import GppBadIcon from '@mui/icons-material/GppBad';
import GppMaybeIcon from '@mui/icons-material/GppMaybe';
import ArrowCircleUpIcon from '@mui/icons-material/ArrowCircleUp';
import ArrowCircleDownIcon from '@mui/icons-material/ArrowCircleDown';
import PageHeader from '../components/PageHeader';
import WeekYearSelector from '../components/WeekYearSelector';
import NoLeague from '../components/NoLeague';
import SpreadRelease from '../components/SpreadRelease';
import TeamHelmet from '../components/sports/TeamHelmet';
import UserPicksMatrix from '../components/UserPicksMatrix';
import PickDialog from '../components/PickDialog';
import FieldPosition from '../components/FieldPosition';
import { useSession } from '../services/session';
import { useAuth } from '../services/auth';
import { spreadLabel } from '../utils/gameHelpers';
import type { SportAdapter, GameView, WeekState, LoadedScores } from '../services/sportAdapter';

// GameView.gameStatus is canonical GameStatusValue — use === directly, no string parsing

// ─── Icon + color helpers (use pre-computed adapter fields) ──────────────────

function teamWins(game: GameView, team: string, pickType: 'Spread' | 'Over' | 'Under'): boolean | null {
  if (game.gameStatus !== 'final') return null;
  if (pickType === 'Spread') {
    if (game.homeCovers == null) return null;
    return team === game.homeTeam ? game.homeCovers : !game.homeCovers;
  }
  if (game.overWins == null) return null;
  return pickType === 'Over' ? game.overWins : !game.overWins;
}

function coverIcon(game: GameView, team: string, pickType: 'Spread' | 'Over' | 'Under') {
  const wins = teamWins(game, team, pickType);
  if (wins == null) return <GppMaybeIcon color="disabled" />;
  return wins ? <GppGoodIcon color="success" /> : <GppBadIcon color="error" />;
}

function badgeColor(game: GameView, team: string, pickType: 'Spread' | 'Over' | 'Under'): 'success' | 'error' | 'info' | 'default' {
  if (game.gameStatus !== 'final') return 'info';
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

  const [loading, setLoading] = useState(true);
  const [data, setData] = useState<LoadedScores | null>(null);
  const [isCurrentWeek, setIsCurrentWeek] = useState(true);
  const [maxWeek, setMaxWeek] = useState(adapter.weekSelectorConfig.maxRegularSeasonWeek);
  const [maxSeason, setMaxSeason] = useState(adapter.weekSelectorConfig.minSeason);
  const [isPageVisible, setIsPageVisible] = useState(true);
  const [showMatrixView, setShowMatrixView] = useState(false);
  const [showOnlyMyPicks, setShowOnlyMyPicks] = useState(false);
  const [dialogState, setDialogState] = useState<{
    open: boolean; teamAbbr: string; logo: string;
    userNames: string[]; userNamesOver: string[]; userNamesUnder: string[];
  } | null>(null);

  const reload = useCallback(async () => {
    if (!currentLeague || !user?.userId) { setLoading(false); return; }
    setLoading(true);
    try {
      const result = await adapter.loadCurrentScores(currentLeague, user.userId);
      const fp = (d: typeof result) => d.games.map(g => `${g.id}:${g.homeScore}:${g.awayScore}:${g.gameStatus}`).join('|');
      setData(prev => prev && fp(prev) === fp(result) ? prev : result);
      setIsCurrentWeek(true);
      setMaxWeek(result.maxWeek);
      setMaxSeason(result.maxSeason);
    } finally {
      setLoading(false);
    }
  }, [currentLeague, user?.userId, adapter]);

  const loadHistorical = useCallback(async (state: WeekState) => {
    if (!currentLeague || !user?.userId) return;
    setLoading(true);
    try {
      const result = await adapter.loadHistoricalScores(currentLeague, user.userId, state);
      setData(result);
    } finally {
      setLoading(false);
    }
  }, [currentLeague, user?.userId, adapter]);

  // Page visibility
  useEffect(() => {
    const h = () => setIsPageVisible(!document.hidden);
    document.addEventListener('visibilitychange', h);
    return () => document.removeEventListener('visibilitychange', h);
  }, []);

  // Load + poll
  useEffect(() => {
    if (!isCurrentWeek || !isPageVisible || !leaguesLoaded) return;
    void reload();
    if (adapter.pollIntervalMs <= 0) return;
    const interval = setInterval(() => void reload(), data?.hasActiveGames ? adapter.pollIntervalMs : adapter.pollIntervalMs * 4);
    return () => clearInterval(interval);
  }, [reload, isCurrentWeek, isPageVisible, leaguesLoaded, data?.hasActiveGames, adapter.pollIntervalMs]);

  const handleWeekChange = (week: number, meta?: { isPostSeason?: boolean }) => {
    const isPostSeason = meta?.isPostSeason ?? data?.isPostSeason ?? false;
    setIsCurrentWeek(false);
    void loadHistorical({ season: data?.season ?? new Date().getFullYear(), week, isPostSeason });
  };
  const handleSeasonChange = (season: number) => {
    setIsCurrentWeek(false);
    void loadHistorical({ season, week: data?.week ?? 1, isPostSeason: data?.isPostSeason ?? false });
  };
  const handleSeasonTypeChange = (isPostSeason: boolean) => {
    setIsCurrentWeek(false);
    void loadHistorical({ season: data?.season ?? new Date().getFullYear(), week: data?.week ?? 1, isPostSeason });
  };

  // Pick query helpers
  const pickCountForTeam = (gameId: string, team: string, pickType: 'Spread' | 'Over' | 'Under') =>
    (data?.allPicks ?? []).filter(p => p.gameId === gameId && p.team === team && p.pickType === pickType).length;

  const didUserPick = (gameId: string, team: string, pickType: 'Spread' | 'Over' | 'Under' = 'Spread') =>
    (data?.userPicks ?? []).some(p => p.gameId === gameId && p.team === team && p.pickType === pickType);

  const showDialog = (game: GameView, team: string, logo: string, pickType: 'Spread' | 'Over' | 'Under' = 'Spread') => {
    if (!adapter.supportsPickDialog) return;
    const names = (data?.allPicks ?? []).filter(p => p.gameId === game.id && p.team === team && p.pickType === pickType).map(p => p.userName).sort();
    if (!names.length) return;
    setDialogState({
      open: true, teamAbbr: team, logo,
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

  if (loading) return (
    <Box><PageHeader title="Scores" />
      <Box sx={{ display: 'flex', justifyContent: 'center', mt: 8 }}><CircularProgress /></Box>
    </Box>
  );
  if (!currentLeague) return <NoLeague />;
  if (!data?.hasOdds && isCurrentWeek) return <SpreadRelease />;
  if (!data) return null;

  const games = showOnlyMyPicks
    ? (data.games ?? []).filter(g => didUserPick(g.id, g.homeTeam) || didUserPick(g.id, g.awayTeam))
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
        />
        {!isCurrentWeek && (
          <Box sx={{ display: 'flex', justifyContent: 'center', mt: -1, mb: 1 }}>
            <Button size="small" variant="outlined" onClick={() => { setIsCurrentWeek(true); void reload(); }}>
              Current Week
            </Button>
          </Box>
        )}
      </Box>

      <Grid container spacing={2}>
        {/* Controls row */}
        <Grid size={12} sx={{ display: 'flex', justifyContent: 'flex-end', gap: 2 }}>
          {adapter.supportsMatrix && (
            <Button variant="contained" onClick={() => setShowMatrixView(p => !p)}>
              {showMatrixView ? 'Show Standard View' : 'Show As Matrix'}
            </Button>
          )}
          {!showMatrixView && (
            <Button variant="contained" color="secondary" onClick={() => setShowOnlyMyPicks(p => !p)}>
              {showOnlyMyPicks ? 'Show All Games' : 'Show Only My Picks'}
            </Button>
          )}
        </Grid>

        {/* Matrix view */}
        {showMatrixView && adapter.supportsMatrix ? (
          <Grid size={12}>
            <UserPicksMatrix
              users={users}
              picks={(data.allPicks ?? []).map(p => ({
                team: p.team, pick: p.pickType, userName: p.userName,
              }))}
              spreads={matrixSpreads}
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
              const isFinal = game.gameStatus === 'final';
              const isLive = game.gameStatus === 'in_progress' || game.gameStatus === 'halftime';
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
                        {isFinal ? 'Final' : isLive ? 'Live' : new Date(game.gameTime).toLocaleString([], { weekday: 'short', month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' })}
                      </Typography>
                      <Typography variant="h6">{isFinal || isLive ? game.homeScore : ''}</Typography>
                      <TeamHelmet abbr={game.homeTeam} size={50} flipped />
                    </Stack>

                    {/* Field position (NFL only) */}
                    {game.situation != null && (
                      <FieldPosition situation={isLive && game.situation ? { downDistanceText: game.situation, shortDownDistanceText: game.situation, possessionText: '', homeTimeouts: 3, awayTimeouts: 3, isRedZone: false, possession: '' } : null} />
                    )}

                    {/* Away team pick row */}
                    <Stack direction="row" alignItems="center" sx={{ mt: 2, gap: 1.5, px: 1 }}>
                      {coverIcon(game, game.awayTeam, 'Spread')}
                      <Typography sx={{ minWidth: 40, fontWeight: 600 }}>{game.awayTeam}</Typography>
                      <Box sx={{ flexGrow: 1, display: 'flex', justifyContent: 'flex-end' }}>
                        <Typography variant="subtitle1">{game.awaySpread != null ? spreadLabel(game.awaySpread) : ''}</Typography>
                      </Box>
                      <Badge
                        data-testid={`badge-${game.awayTeam}-spread`}
                        data-tone={didUserPick(game.id, game.awayTeam) ? 'info' : badgeColor(game, game.awayTeam, 'Spread')}
                        color={didUserPick(game.id, game.awayTeam) ? 'info' : badgeColor(game, game.awayTeam, 'Spread')}
                        overlap="circular"
                        badgeContent={pickCountForTeam(game.id, game.awayTeam, 'Spread')}
                        invisible={(!isFinal && !isLive) || pickCountForTeam(game.id, game.awayTeam, 'Spread') === 0}
                      >
                        <IconButton
                          color={isFinal ? (hc === false ? 'success' : hc === true ? 'error' : 'inherit') : 'inherit'}
                          disabled={(!isFinal && !isLive) || pickCountForTeam(game.id, game.awayTeam, 'Spread') === 0}
                          onClick={() => showDialog(game, game.awayTeam, game.awayLogo ?? '', 'Spread')}
                          size="small"
                        >
                          <PersonIcon />
                        </IconButton>
                      </Badge>
                    </Stack>

                    {/* Home team pick row */}
                    <Stack direction="row" alignItems="center" sx={{ mt: 1.5, gap: 1.5, px: 1 }}>
                      {coverIcon(game, game.homeTeam, 'Spread')}
                      <Typography sx={{ minWidth: 40, fontWeight: 600 }}>{game.homeTeam}</Typography>
                      <Box sx={{ flexGrow: 1, display: 'flex', justifyContent: 'flex-end' }}>
                        <Typography variant="subtitle1">{game.homeSpread != null ? spreadLabel(game.homeSpread) : ''}</Typography>
                      </Box>
                      <Badge
                        data-testid={`badge-${game.homeTeam}-spread`}
                        data-tone={didUserPick(game.id, game.homeTeam) ? 'info' : badgeColor(game, game.homeTeam, 'Spread')}
                        color={didUserPick(game.id, game.homeTeam) ? 'info' : badgeColor(game, game.homeTeam, 'Spread')}
                        overlap="circular"
                        badgeContent={pickCountForTeam(game.id, game.homeTeam, 'Spread')}
                        invisible={(!isFinal && !isLive) || pickCountForTeam(game.id, game.homeTeam, 'Spread') === 0}
                      >
                        <IconButton
                          color={isFinal ? (hc === true ? 'success' : hc === false ? 'error' : 'inherit') : 'inherit'}
                          disabled={(!isFinal && !isLive) || pickCountForTeam(game.id, game.homeTeam, 'Spread') === 0}
                          onClick={() => showDialog(game, game.homeTeam, game.homeLogo ?? '', 'Spread')}
                          size="small"
                        >
                          <PersonIcon />
                        </IconButton>
                      </Badge>
                    </Stack>

                    {/* Postseason O/U row */}
                    {isPostSeason && game.overUnder != null && (
                      <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ mt: 2.5, px: 1, gap: 1 }}>
                        <Badge color={didUserPick(game.id, game.homeTeam, 'Over') ? 'info' : badgeColor(game, game.homeTeam, 'Over')} overlap="circular"
                          badgeContent={pickCountForTeam(game.id, game.homeTeam, 'Over')}
                          invisible={(!isFinal && !isLive) || pickCountForTeam(game.id, game.homeTeam, 'Over') === 0}>
                          <IconButton size="small"
                            disabled={(!isFinal && !isLive) || pickCountForTeam(game.id, game.homeTeam, 'Over') === 0}
                            onClick={() => showDialog(game, game.homeTeam, game.homeLogo ?? '', 'Over')}>
                            <PersonIcon />
                          </IconButton>
                        </Badge>
                        <ArrowCircleUpIcon sx={{ color: isFinal ? (ov ? 'success.main' : 'error.main') : 'text.secondary', flexShrink: 0 }} />
                        <Typography variant="subtitle1" sx={{ minWidth: 36, textAlign: 'center' }}>{game.overUnder}</Typography>
                        <ArrowCircleDownIcon sx={{ color: isFinal ? (!ov ? 'success.main' : 'error.main') : 'text.secondary', flexShrink: 0 }} />
                        <Badge color={didUserPick(game.id, game.homeTeam, 'Under') ? 'info' : badgeColor(game, game.homeTeam, 'Under')} overlap="circular"
                          badgeContent={pickCountForTeam(game.id, game.homeTeam, 'Under')}
                          invisible={(!isFinal && !isLive) || pickCountForTeam(game.id, game.homeTeam, 'Under') === 0}>
                          <IconButton size="small"
                            disabled={(!isFinal && !isLive) || pickCountForTeam(game.id, game.homeTeam, 'Under') === 0}
                            onClick={() => showDialog(game, game.homeTeam, game.homeLogo ?? '', 'Under')}>
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

      {dialogState && adapter.supportsPickDialog && (
        <PickDialog
          open={dialogState.open}
          onClose={() => setDialogState(null)}
          teamAbbr={dialogState.teamAbbr}
          logo={dialogState.logo}
          userNames={dialogState.userNames}
          userNamesOver={dialogState.userNamesOver}
          userNamesUnder={dialogState.userNamesUnder}
        />
      )}
    </Box>
  );
}
