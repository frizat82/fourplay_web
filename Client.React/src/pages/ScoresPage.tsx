import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Badge,
  Box,
  Button,
  CircularProgress,
  Grid,
  IconButton,
  Paper,
  Stack,
  Typography,
} from '@mui/material';
import PersonIcon from '@mui/icons-material/Person';
import SportsFootballIcon from '@mui/icons-material/SportsFootball';
import GppGoodIcon from '@mui/icons-material/GppGood';
import GppBadIcon from '@mui/icons-material/GppBad';
import ArrowCircleUpIcon from '@mui/icons-material/ArrowCircleUp';
import ArrowCircleDownIcon from '@mui/icons-material/ArrowCircleDown';
import PageHeader from '../components/PageHeader';
import WeekYearSelector from '../components/WeekYearSelector';
import NoLeague from '../components/NoLeague';
import SpreadRelease from '../components/SpreadRelease';
import ScoreTicker from '../components/ScoreTicker';
import UserPicksMatrix from '../components/UserPicksMatrix';
import PickDialog from '../components/PickDialog';
import { useSession } from '../services/session';
import { useAuth } from '../services/auth';
import { getScores, getLiveGames, getWeekScores } from '../api/espn';
import { calculateSpreadBatch, doOddsExist, getLeaguePicks } from '../api/league';
import type { EspnScores, Event, Competition } from '../types/espn';
import type { LiveGame } from '../types/liveGame';
import FieldPosition from '../components/FieldPosition';
import type { BatchSpreadCalculationRequest, NflPickDto, SpreadCalculationResponse, PickType } from '../types/picks';
import {
  displayDetails,
  getAwayTeamAbbr,
  getAwayTeamLogo,
  getAwayTeamScore,
  getHomeTeamAbbr,
  getHomeTeamLogo,
  getHomeTeamScore,
  getScoreEvents,
  getWeekFromEspnWeek,
  getEspnRequiredPicks,
  isAfterKickoff,
  isGameStarted,
  isHalfTime,
  isPostSeason as isPostSeasonHelper,
  isRedZone,
  shouldShowGamePicks,
  hasPossession,
} from '../utils/gameHelpers';

export default function ScoresPage() {
  const { currentLeague } = useSession();
  const { user } = useAuth();
  const [loading, setLoading] = useState(true);
  const [scores, setScores] = useState<EspnScores | null>(null);
  const [isPostSeason, setIsPostSeason] = useState(false);
  const [week, setWeek] = useState(0);
  const [season, setSeason] = useState(new Date().getFullYear());
  const [hasOdds, setHasOdds] = useState(false);
  const [picks, setPicks] = useState<NflPickDto[]>([]);
  const [spreadCache, setSpreadCache] = useState<Record<string, SpreadCalculationResponse>>({});
  const [showMatrixView, setShowMatrixView] = useState(false);
  const [showOnlyMyPicks, setShowOnlyMyPicks] = useState(false);
  const [isCurrentWeek, setIsCurrentWeek] = useState(true);
  const [hasActiveGames, setHasActiveGames] = useState(false);
  const [liveGames, setLiveGames] = useState<LiveGame[]>([]);
  const [isPageVisible, setIsPageVisible] = useState(true);
  const [dialogState, setDialogState] = useState<{
    open: boolean;
    teamAbbr: string;
    logo: string;
    userNames: string[];
    userNamesOver: string[];
    userNamesUnder: string[];
  } | null>(null);

  const loadScoresWithRetry = useCallback(async (maxRetries = 5, delayMs = 500) => {
    let attempt = 0;
    let data: EspnScores | null = null;
    while ((!data?.events || data.events.length === 0) && attempt < maxRetries) {
      data = await getScores();
      if (data?.events && data.events.length > 0) break;
      await new Promise((resolve) => setTimeout(resolve, delayMs));
      attempt += 1;
    }
    return data;
  }, []);

  const loadSpreadCalculations = useCallback(
    async (scoresData: EspnScores, leagueId: number, season: number, weekNumber: number, postSeason: boolean) => {
      if (!scoresData.events) return;
      const request: BatchSpreadCalculationRequest = { calculations: [] };
      for (const scoreEvent of scoresData.events) {
        for (const competition of scoreEvent.competitions) {
          const homeTeam = getHomeTeamAbbr(competition);
          const awayTeam = getAwayTeamAbbr(competition);
          const homeScore = getHomeTeamScore(competition);
          const awayScore = getAwayTeamScore(competition);
          request.calculations.push({ team: homeTeam, pickTeamScore: homeScore, otherTeamScore: awayScore });
          request.calculations.push({ team: awayTeam, pickTeamScore: awayScore, otherTeamScore: homeScore });
        }
      }
      const response = await calculateSpreadBatch(
        leagueId,
        season,
        getWeekFromEspnWeek(weekNumber, postSeason),
        request
      );
      setSpreadCache(response.results ?? {});
    },
    []
  );

  const loadHistoricalWeek = useCallback(
    async (selectedSeason: number, selectedWeek: number, selectedIsPostSeason: boolean) => {
      if (!currentLeague) {
        setLoading(false);
        return;
      }

      setLoading(true);
      const data = await getWeekScores(selectedWeek, selectedSeason, selectedIsPostSeason);
      if (!data?.events || data.events.length === 0) {
        setScores(null);
        setLoading(false);
        return;
      }

      setScores(data);
      setIsPostSeason(selectedIsPostSeason);
      setWeek(selectedWeek);
      setSeason(selectedSeason);

      const nflWeek = getWeekFromEspnWeek(selectedWeek, selectedIsPostSeason);
      const [oddsExist, picksResult] = await Promise.all([
        doOddsExist(currentLeague, selectedSeason, nflWeek),
        getLeaguePicks(currentLeague, selectedSeason, nflWeek),
      ]);
      setHasOdds(oddsExist);
      setPicks(picksResult ?? []);

      if (oddsExist) {
        await loadSpreadCalculations(data, currentLeague, selectedSeason, selectedWeek, selectedIsPostSeason);
      }

      setLoading(false);
    },
    [currentLeague, loadSpreadCalculations]
  );

  const reload = useCallback(async () => {
    if (!currentLeague) {
      setLoading(false);
      return;
    }

    setLoading(true);
    const [data, liveGamesData] = await Promise.all([loadScoresWithRetry(), getLiveGames()]);
    setLiveGames(liveGamesData ?? []);
    if (!data?.season || !data.week) {
      setScores(null);
      setLoading(false);
      return;
    }

    setScores(data);
    const postSeason = isPostSeasonHelper(data);
    setIsPostSeason(postSeason);
    setWeek(data.week.number);
    setSeason(data.season.year);
    setIsCurrentWeek(true);

    const seasonYear = data.season.year;
    const oddsExist = await doOddsExist(currentLeague, seasonYear, getWeekFromEspnWeek(data.week.number, postSeason));
    setHasOdds(oddsExist);

    const picksResult = await getLeaguePicks(currentLeague, seasonYear, getWeekFromEspnWeek(data.week.number, postSeason));
    setPicks(picksResult ?? []);

    if (oddsExist) {
      await loadSpreadCalculations(data, currentLeague, seasonYear, data.week.number, postSeason);
    }

    setLoading(false);
  }, [currentLeague, loadScoresWithRetry, loadSpreadCalculations]);

  const handleSeasonChange = (newSeason: number) => {
    setSeason(newSeason);
    setIsCurrentWeek(false);
    void loadHistoricalWeek(newSeason, week, isPostSeason);
  };

  const handleWeekChange = (newWeek: number) => {
    setWeek(newWeek);
    setIsCurrentWeek(false);
    void loadHistoricalWeek(season, newWeek, isPostSeason);
  };

  const handleSeasonTypeChange = (newIsPostSeason: boolean) => {
    setIsPostSeason(newIsPostSeason);
    setIsCurrentWeek(false);
    void loadHistoricalWeek(season, week, newIsPostSeason);
  };

  // Detect active games to enable more frequent updates
  const detectActiveGames = useCallback(() => {
    if (!scores?.events) return false;
    return scores.events.some((event) =>
      event.competitions.some((comp) => {
        const statusName = comp.status?.type?.name || '';
        // Active if in-progress or in halftime (not scheduled, final, etc)
        return statusName === 'status_in_progress' || statusName === 'status_halftime' || 
               statusName === 'status_end_period';
      })
    );
  }, [scores]);

  // Update active games state
  useEffect(() => {
    setHasActiveGames(detectActiveGames());
  }, [scores, detectActiveGames]);

  // Page visibility detection for smart polling
  useEffect(() => {
    const handleVisibilityChange = () => {
      setIsPageVisible(!document.hidden);
    };

    document.addEventListener('visibilitychange', handleVisibilityChange);
    return () => document.removeEventListener('visibilitychange', handleVisibilityChange);
  }, []);

  // Smart polling: faster when games are active and page is visible
  useEffect(() => {
    if (!isCurrentWeek || !isPageVisible) {
      return; // Don't reload/poll if viewing historical week or page is hidden
    }

    void reload();

    // Determine polling interval based on conditions
    let pollInterval: number;
    if (hasActiveGames) {
      pollInterval = 30 * 1000; // 30 seconds when games are actively playing
    } else {
      pollInterval = 2 * 60 * 1000; // 2 minutes when waiting for games to start
    }

    const interval = setInterval(() => {
      void reload();
    }, pollInterval);

    return () => clearInterval(interval);
  }, [reload, isCurrentWeek, isPageVisible, hasActiveGames]);

  const showNoLeague = !loading && !currentLeague;
  const showNoOdds = !loading && currentLeague && !hasOdds;

  const getWinner = (teamAbbr: string, pickType: PickType = 'Spread') => {
    const calc = spreadCache[teamAbbr];
    if (!calc) return null;
    if (pickType === 'Spread') return calc.isWinner;
    if (pickType === 'Over') return calc.isOverWinner;
    return calc.isUnderWinner;
  };

  const getColor = (competition: Competition, teamAbbr: string, pickType: PickType = 'Spread'): 'success' | 'error' => {
    if (!isGameStarted(competition)) return 'success';
    const winner = getWinner(teamAbbr, pickType);
    if (winner === null) return 'error';
    return winner ? 'success' : 'error';
  };

  const getIcon = (competition: Competition, teamAbbr: string, pickType: PickType = 'Spread') => {
    if (!isGameStarted(competition)) return <GppGoodIcon />;
    const winner = getWinner(teamAbbr, pickType);
    if (winner === null) return <GppBadIcon />;
    return winner ? <GppGoodIcon color="success" /> : <GppBadIcon color="error" />;
  };

  const didUserPick = (teamAbbr: string, pickType: PickType = 'Spread') => {
    if (!user?.name) return false;
    return picks.some((p) => p.team === teamAbbr && p.pick === pickType && p.userName === user.name);
  };

  const getBadgeColor = (competition: Competition, teamAbbr: string, pickType: PickType = 'Spread'): 'primary' | 'success' | 'error' => {
    if (!isGameStarted(competition)) return 'primary';
    const winner = getWinner(teamAbbr, pickType);
    if (winner === null) return 'primary';
    return winner ? 'success' : 'error';
  };

  const getPickBadgeColor = (competition: Competition, teamAbbr: string, pickType: PickType = 'Spread'): 'primary' | 'success' | 'error' | 'info' => {
    if (didUserPick(teamAbbr, pickType)) return 'info';
    return getBadgeColor(competition, teamAbbr, pickType);
  };

  const badgeColorToSx = (color: 'primary' | 'success' | 'error' | 'info') =>
    color === 'success' ? 'success.main' : color === 'error' ? 'error.main' : 'text.secondary';

  const didUserPickCompetition = (competition: Competition) => {
    if (!user?.name) return false;
    const away = getAwayTeamAbbr(competition);
    const home = getHomeTeamAbbr(competition);
    return picks.some((p) => (p.team === away || p.team === home) && p.userName === user.name);
  };

  const getUserPicksCount = (competition: Competition, teamAbbr: string, pickType: PickType = 'Spread') => {
    if (!isGameStarted(competition) && !isAfterKickoff(competition)) return 0;
    return picks.filter((p) => p.team === teamAbbr && p.pick === pickType).length;
  };

  const users = useMemo(() => Array.from(new Set(picks.map((p) => p.userName))), [picks]);

  const getActiveGamePicks = () => {
    const active: NflPickDto[] = [];
    for (const scoreEvent of getScoreEvents(scores)) {
      for (const competition of scoreEvent.competitions) {
        const showGame = shouldShowGamePicks(competition);
        if (showGame) {
          const away = getAwayTeamAbbr(competition);
          const home = getHomeTeamAbbr(competition);
          active.push(...picks.filter((p) => p.team === away || p.team === home));
        }
      }
    }
    return active;
  };

  const getActiveSpreads = () => {
    const active: Record<string, SpreadCalculationResponse> = {};
    if (!scores?.events) return active;
    for (const scoreEvent of scores.events) {
      for (const competition of scoreEvent.competitions) {
        if (!isGameStarted(competition)) continue;
        const away = getAwayTeamAbbr(competition);
        const home = getHomeTeamAbbr(competition);
        if (spreadCache[away]) active[away] = spreadCache[away];
        if (spreadCache[home]) active[home] = spreadCache[home];
      }
    }
    return active;
  };

  const showDialog = (teamAbbr: string, logo: string, pickType: PickType = 'Spread') => {
    const names = picks.filter((p) => p.team === teamAbbr && p.pick === pickType).map((p) => p.userName).sort((a, b) => a.localeCompare(b));
    if (names.length === 0) return;
    setDialogState({
      open: true,
      teamAbbr,
      logo,
      userNames: pickType === 'Spread' ? names : [],
      userNamesOver: pickType === 'Over' ? names : [],
      userNamesUnder: pickType === 'Under' ? names : [],
    });
  };

  if (loading) {
    return (
      <Box>
        <PageHeader title="Scores" />
        <Box sx={{ display: 'flex', justifyContent: 'center', mt: 8 }}>
          <CircularProgress />
        </Box>
      </Box>
    );
  }

  if (showNoLeague) {
    return <NoLeague />;
  }

  if (showNoOdds) {
    return <SpreadRelease />;
  }

  return (
    <Box>
      <PageHeader title="Scores" />
      {scores && !showNoLeague && !showNoOdds && (
        <Box sx={{ mb: 3 }}>
          <WeekYearSelector
            season={season}
            week={week}
            isPostSeason={isPostSeason}
            onSeasonChange={handleSeasonChange}
            onWeekChange={handleWeekChange}
            onSeasonTypeChange={handleSeasonTypeChange}
          />
        </Box>
      )}
      {scores && (
        <Grid container spacing={2}>
          {!showNoLeague && !showNoOdds && (
            <Grid size={12} sx={{ display: 'flex', justifyContent: 'flex-end', gap: 2 }}>
              <Button variant="contained" onClick={() => setShowMatrixView((prev) => !prev)}>
                {showMatrixView ? 'Show Standard View' : 'Show As Matrix'}
              </Button>
              {!showMatrixView && (
                <Button variant="contained" color="secondary" onClick={() => setShowOnlyMyPicks((prev) => !prev)}>
                  {showOnlyMyPicks ? 'Show All Games' : 'Show Only My Picks'}
                </Button>
              )}
            </Grid>
          )}

          {showMatrixView ? (
            <>
              <Grid size={12}>
                <UserPicksMatrix
                  users={users}
                  picks={getActiveGamePicks()}
                  spreads={getActiveSpreads()}
                  requiredPicks={
                    users.length > 0
                      ? Math.max(getEspnRequiredPicks(week, isPostSeason), Math.max(...users.map((u) => picks.filter((p) => p.userName === u).length)))
                      : getEspnRequiredPicks(week, isPostSeason)
                  }
                />
              </Grid>
            </>
          ) : (
            <>
              {getScoreEvents(scores)
                .sort((a, b) => new Date(a.date).getTime() - new Date(b.date).getTime())
                .map((scoreEvent: Event) =>
                  scoreEvent.competitions
                    .sort((a, b) => getHomeTeamAbbr(a).localeCompare(getHomeTeamAbbr(b)))
                    .map((competition: Competition) => {
                      if (showOnlyMyPicks && !didUserPickCompetition(competition)) return null;

                      const awayAbbr = getAwayTeamAbbr(competition);
                      const homeAbbr = getHomeTeamAbbr(competition);
                      const showGamePicks = shouldShowGamePicks(competition);

                      return (
                        <Grid size={{ xs: 12, md: 6, lg: 4 }} key={`${competition.id}-${awayAbbr}-${homeAbbr}`}>
                          <Paper className={isRedZone(competition) ? 'red-zone-border' : ''} sx={{ p: 2 }}>
                            <Stack direction="row" alignItems="center" justifyContent="space-between">
                              {hasPossession(competition, awayAbbr) ? <SportsFootballIcon /> : <span />}
                              <img src={getAwayTeamLogo(competition)} width={50} />
                              <Typography variant="h6">{getAwayTeamScore(competition)}</Typography>
                              <Typography variant="body2">{displayDetails(competition)}</Typography>
                              <Typography variant="h6">{getHomeTeamScore(competition)}</Typography>
                              <img src={getHomeTeamLogo(competition)} width={50} />
                              {hasPossession(competition, homeAbbr) ? <SportsFootballIcon /> : <span />}
                            </Stack>

                            {!isHalfTime(competition) && (
                              <FieldPosition
                                situation={liveGames.find(
                                  (g) => g.homeTeam === homeAbbr && g.awayTeam === awayAbbr
                                )?.situation ?? null}
                              />
                            )}

                            <Stack direction="row" alignItems="center" sx={{ mt: 3, gap: 1.5, px: 1 }}>
                              {getIcon(competition, awayAbbr)}
                              <Typography sx={{ minWidth: 36, fontWeight: 600 }}>{awayAbbr}</Typography>
                              <Box sx={{ flexGrow: 1, display: 'flex', justifyContent: 'flex-end' }}>
                                <Typography variant="subtitle1">
                                  {spreadCache[awayAbbr]?.spread ?? ''}
                                </Typography>
                              </Box>
                              <Badge
                                data-testid={`badge-${awayAbbr}-spread`}
                                data-tone={getPickBadgeColor(competition, awayAbbr)}
                                color={getPickBadgeColor(competition, awayAbbr)}
                                overlap="circular"
                                badgeContent={getUserPicksCount(competition, awayAbbr)}
                                invisible={!showGamePicks || getUserPicksCount(competition, awayAbbr) === 0}
                              >
                                <IconButton
                                  color={getColor(competition, awayAbbr)}
                                  disabled={!showGamePicks || getUserPicksCount(competition, awayAbbr) === 0}
                                  onClick={() => showDialog(awayAbbr, getAwayTeamLogo(competition))}
                                >
                                  <PersonIcon />
                                </IconButton>
                              </Badge>
                            </Stack>

                            <Stack direction="row" alignItems="center" sx={{ mt: 1.5, gap: 1.5, px: 1 }}>
                              {getIcon(competition, homeAbbr)}
                              <Typography sx={{ minWidth: 36, fontWeight: 600 }}>{homeAbbr}</Typography>
                              <Box sx={{ flexGrow: 1, display: 'flex', justifyContent: 'flex-end' }}>
                                <Typography variant="subtitle1">
                                  {spreadCache[homeAbbr]?.spread ?? ''}
                                </Typography>
                              </Box>
                              <Badge
                                data-testid={`badge-${homeAbbr}-spread`}
                                data-tone={getPickBadgeColor(competition, homeAbbr)}
                                color={getPickBadgeColor(competition, homeAbbr)}
                                overlap="circular"
                                badgeContent={getUserPicksCount(competition, homeAbbr)}
                                invisible={!showGamePicks || getUserPicksCount(competition, homeAbbr) === 0}
                              >
                                <IconButton
                                  color={getColor(competition, homeAbbr)}
                                  disabled={!showGamePicks || getUserPicksCount(competition, homeAbbr) === 0}
                                  onClick={() => showDialog(homeAbbr, getHomeTeamLogo(competition))}
                                >
                                  <PersonIcon />
                                </IconButton>
                              </Badge>
                            </Stack>

                            {isPostSeason && (
                              <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ mt: 2.5, px: 1, gap: 1 }}>
                                <Badge
                                  data-testid={`badge-${homeAbbr}-over`}
                                  data-tone={getPickBadgeColor(competition, homeAbbr, 'Over')}
                                  color={getPickBadgeColor(competition, homeAbbr, 'Over')}
                                  overlap="circular"
                                  badgeContent={getUserPicksCount(competition, homeAbbr, 'Over')}
                                  invisible={!showGamePicks || getUserPicksCount(competition, homeAbbr, 'Over') === 0}
                                >
                                  <IconButton
                                    color={getColor(competition, homeAbbr, 'Over')}
                                    disabled={!showGamePicks || getUserPicksCount(competition, homeAbbr, 'Over') === 0}
                                    onClick={() => showDialog(homeAbbr, getHomeTeamLogo(competition), 'Over')}
                                  >
                                    <PersonIcon />
                                  </IconButton>
                                </Badge>
                                <ArrowCircleUpIcon sx={{ color: badgeColorToSx(getBadgeColor(competition, homeAbbr, 'Over')), flexShrink: 0 }} />
                                <Typography variant="subtitle1" sx={{ minWidth: 36, textAlign: 'center' }}>{spreadCache[homeAbbr]?.over ?? ''}</Typography>
                                <Typography variant="subtitle1" sx={{ minWidth: 36, textAlign: 'center' }}>{spreadCache[homeAbbr]?.under ?? ''}</Typography>
                                <ArrowCircleDownIcon sx={{ color: badgeColorToSx(getBadgeColor(competition, homeAbbr, 'Under')), flexShrink: 0 }} />
                                <Badge
                                  data-testid={`badge-${homeAbbr}-under`}
                                  data-tone={getPickBadgeColor(competition, homeAbbr, 'Under')}
                                  color={getPickBadgeColor(competition, homeAbbr, 'Under')}
                                  overlap="circular"
                                  badgeContent={getUserPicksCount(competition, homeAbbr, 'Under')}
                                  invisible={!showGamePicks || getUserPicksCount(competition, homeAbbr, 'Under') === 0}
                                >
                                  <IconButton
                                    color={getColor(competition, homeAbbr, 'Under')}
                                    disabled={!showGamePicks || getUserPicksCount(competition, homeAbbr, 'Under') === 0}
                                    onClick={() => showDialog(homeAbbr, getAwayTeamLogo(competition), 'Under')}
                                  >
                                    <PersonIcon />
                                  </IconButton>
                                </Badge>
                              </Stack>
                            )}

                            {(getColor(competition, awayAbbr) === 'error' ||
                              getColor(competition, homeAbbr) === 'error' ||
                              getColor(competition, homeAbbr, 'Over') === 'error' ||
                              getColor(competition, homeAbbr, 'Under') === 'error') && (
                              <ScoreTicker
                                competition={competition}
                                homeSpread={spreadCache[homeAbbr]?.spread ?? null}
                                awaySpread={spreadCache[awayAbbr]?.spread ?? null}
                                over={spreadCache[homeAbbr]?.over ?? null}
                                under={spreadCache[homeAbbr]?.under ?? null}
                                isPostSeason={isPostSeason}
                              />
                            )}
                          </Paper>
                        </Grid>
                      );
                    })
                )}
            </>
          )}
        </Grid>
      )}

      {dialogState && (
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
