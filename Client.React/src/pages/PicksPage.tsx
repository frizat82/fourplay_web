import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Box,
  Button,
  CircularProgress,
  Grid,
  Paper,
  Stack,
  Typography,
} from '@mui/material';
import CheckroomIcon from '@mui/icons-material/Checkroom';
import PageHeader from '../components/PageHeader';
import WeekYearSelector from '../components/WeekYearSelector';
import NoLeague from '../components/NoLeague';
import SpreadRelease from '../components/SpreadRelease';
import WeatherIcon from '../components/WeatherIcon';
import { useSession } from '../services/session';
import { useAuth } from '../services/auth';
import { getWeekScores, loadScoresWithRetry } from '../api/espn';
import { addPicks, doOddsExist, getUserPicks, spreadBatch } from '../api/league';
import { getAllJerseys } from '../api/jersey';
import type { EspnScores, Competition, Event } from '../types/espn';
import type { NflPickDto, PickType, SpreadResponse, BatchSpreadRequest } from '../types/picks';
import {
  getAwayTeamAbbr,
  getAwayTeamLogo,
  // getAwayTeamScore,
  getAwayTeam,
  getHomeTeamAbbr,
  getHomeTeamLogo,
  // getHomeTeamScore,
  getHomeTeam,
  getTeamRecord,
  getWeekFromEspnWeek,
  getEspnRequiredPicks,
  isAfterKickoff,
  isGameStarted,
  isPostSeason as isPostSeasonHelper,
  displayDetails,
} from '../utils/gameHelpers';
import { useToast } from '../services/toast';

export default function PicksPage() {
  const { currentLeague } = useSession();
  const { user } = useAuth();
  const toast = useToast();

  const [loading, setLoading] = useState(true);
  const [scores, setScores] = useState<EspnScores | null>(null);
  const [isPostSeason, setIsPostSeason] = useState(false);
  const [week, setWeek] = useState(0);
  const [season, setSeason] = useState(new Date().getFullYear());
  const [hasOdds, setHasOdds] = useState(false);
  const [spreadCache, setSpreadCache] = useState<Record<string, SpreadResponse>>({});
  const [existingPicks, setExistingPicks] = useState<Set<string>>(new Set());
  const [userPicks, setUserPicks] = useState<Set<string>>(new Set());
  const [storingPicks, setStoringPicks] = useState(false);
  const [showJerseys, setShowJerseys] = useState(false);
  const [jerseyCache, setJerseyCache] = useState<Record<string, string>>({});
  const [isCurrentWeek, setIsCurrentWeek] = useState(true);
  const [isPageVisible, setIsPageVisible] = useState(true);
  const pickButtonSx = { minWidth: 80, height: 44, textTransform: 'none', fontSize: '0.85rem', flexShrink: 0 };

  const pickKey = (pick: NflPickDto) =>
    `${pick.team}|${pick.pick}|${pick.season}|${pick.nflWeek}|${pick.userId}|${pick.leagueId}`;

  const loadAllSpreads = useCallback(
    async (scoresData: EspnScores, leagueId: number, season: number, weekNumber: number, postSeason: boolean) => {
      const request: BatchSpreadRequest = { requests: [] };
      for (const scoreEvent of scoresData.events ?? []) {
        for (const competition of scoreEvent.competitions) {
          const homeTeam = getHomeTeamAbbr(competition);
          const awayTeam = getAwayTeamAbbr(competition);
          request.requests.push({ team: homeTeam });
          request.requests.push({ team: awayTeam });
        }
      }
      const response = await spreadBatch(leagueId, season, getWeekFromEspnWeek(weekNumber, postSeason), request);
      setSpreadCache(response.responses ?? {});
    },
    []
  );

  const loadJerseys = useCallback(async (season: number, weekNumber: number) => {
    try {
      const jerseys = await getAllJerseys(season, weekNumber);
      setJerseyCache(jerseys ?? {});
    } catch {
      setJerseyCache({});
    }
  }, []);

  const loadHistoricalWeek = useCallback(
    async (selectedSeason: number, selectedWeek: number, selectedIsPostSeason: boolean) => {
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

    if (!currentLeague) {
      setLoading(false);
      return;
    }

    const nflWeek = getWeekFromEspnWeek(selectedWeek, selectedIsPostSeason);

    const [picksResult, oddsExist] = await Promise.all([
      user?.userId ? getUserPicks(user.userId, currentLeague, selectedSeason, nflWeek) : Promise.resolve([]),
      doOddsExist(currentLeague, selectedSeason, nflWeek),
    ]);
    setExistingPicks(new Set(picksResult.map((p) => pickKey(p))));
    setHasOdds(oddsExist);

      if (oddsExist) {
        await loadAllSpreads(data, currentLeague, selectedSeason, selectedWeek, selectedIsPostSeason);
      }

      await loadJerseys(selectedSeason, selectedWeek);

      setLoading(false);
    },
    [currentLeague, loadAllSpreads, loadJerseys, user?.userId]
  );

  const reload = useCallback(async () => {
    setLoading(true);

    const data = await loadScoresWithRetry();
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

    if (!currentLeague) {
      setLoading(false);
      return;
    }

    const seasonYear = data.season.year;
    const nflWeek = getWeekFromEspnWeek(data.week.number, postSeason);
    const [picksResult, oddsExist] = await Promise.all([
      user?.userId ? getUserPicks(user.userId, currentLeague, seasonYear, nflWeek) : Promise.resolve([]),
      doOddsExist(currentLeague, seasonYear, nflWeek),
    ]);
    setExistingPicks(new Set(picksResult.map((p) => pickKey(p))));
    setHasOdds(oddsExist);

    if (oddsExist) {
      await loadAllSpreads(data, currentLeague, seasonYear, data.week.number, postSeason);
    }

    await loadJerseys(seasonYear, data.week.number);

    setLoading(false);
  }, [currentLeague, loadAllSpreads, loadJerseys, user?.userId]);

  const handleSeasonChange = (newSeason: number) => {
    setSeason(newSeason);
    setIsCurrentWeek(false);
    void loadHistoricalWeek(newSeason, week, isPostSeason);
  };

  const handleWeekChange = (newWeek: number, meta?: { isPostSeason?: boolean }) => {
    const newIsPostSeason = meta?.isPostSeason ?? isPostSeason;
    setWeek(newWeek);
    setIsPostSeason(newIsPostSeason);
    setIsCurrentWeek(false);
    void loadHistoricalWeek(season, newWeek, newIsPostSeason);
  };

  const handleSeasonTypeChange = (newIsPostSeason: boolean) => {
    setIsPostSeason(newIsPostSeason);
    setIsCurrentWeek(false);
    void loadHistoricalWeek(season, week, newIsPostSeason);
  };

  const hasActiveGames = useMemo(() => {
    if (!scores?.events) return false;
    return scores.events.some((event) =>
      event.competitions.some((comp) => {
        const statusName = comp.status?.type?.name ?? '';
        return statusName === 'status_in_progress' || statusName === 'status_halftime' ||
               statusName === 'status_end_period';
      })
    );
  }, [scores]);

  // Page visibility detection for smart polling
  useEffect(() => {
    const handleVisibilityChange = () => {
      setIsPageVisible(!document.hidden);
    };

    document.addEventListener('visibilitychange', handleVisibilityChange);
    return () => document.removeEventListener('visibilitychange', handleVisibilityChange);
  }, []);

  useEffect(() => {
    if (!isCurrentWeek || !isPageVisible) {
      return;
    }

    void reload();

    const pollInterval = hasActiveGames ? 30 * 1000 : 2 * 60 * 1000;
    const interval = setInterval(() => {
      void reload();
    }, pollInterval);

    return () => clearInterval(interval);
  }, [reload, isCurrentWeek, isPageVisible, hasActiveGames]);

  const requiredPicks = getEspnRequiredPicks(week, isPostSeason);
  const requiredRemaining = requiredPicks - userPicks.size - existingPicks.size;

  const isPicksLocked = () => requiredRemaining === 0;
  const isClearPicksDisabled = () => userPicks.size === 0;
  const isSubmitDisabled = () => storingPicks || userPicks.size === 0;
  const isGameSelectDisabled = () => storingPicks || requiredRemaining === 0;

  const getSpread = (teamAbbr: string, pickType: PickType = 'Spread') => {
    const calc = spreadCache[teamAbbr];
    if (!calc) return null;
    if (pickType === 'Spread') return calc.spread;
    if (pickType === 'Over') return calc.over;
    return calc.under;
  };

  const competitionToPick = (teamAbbr: string, pickType: PickType): NflPickDto => ({
    id: 0,
    leagueId: currentLeague ?? 0,
    nflWeek: getWeekFromEspnWeek(week, isPostSeason),
    season: scores?.season?.year ?? 0,
    team: teamAbbr,
    userId: user?.userId ?? '',
    userName: user?.name ?? '',
    pick: pickType,
    dateCreated: new Date().toISOString(),
  });

  const isSelected = (teamAbbr: string, pickType: PickType = 'Spread') => {
    const pick = competitionToPick(teamAbbr, pickType);
    const key = pickKey(pick);
    return userPicks.has(key) || existingPicks.has(key);
  };

  const selectPick = (teamAbbr: string, pickType: PickType = 'Spread') => {
    if (isPicksLocked()) return;
    const pick = competitionToPick(teamAbbr, pickType);
    const key = pickKey(pick);
    setUserPicks((prev) => new Set(prev).add(key));
  };

  const unselectPick = (teamAbbr: string, pickType: PickType = 'Spread') => {
    if (isPicksLocked()) return;
    const pick = competitionToPick(teamAbbr, pickType);
    const key = pickKey(pick);
    setUserPicks((prev) => {
      const next = new Set(prev);
      next.delete(key);
      return next;
    });
  };

  const clearPicks = () => setUserPicks(new Set());

  const isGameStartedOrDisabledPicks = (competition: Competition) =>
    isGameStarted(competition) || isGameSelectDisabled() || isAfterKickoff(competition);

  const handleSubmit = async () => {
    if (!user?.userId || !currentLeague || !scores?.season) return;
    setStoringPicks(true);
    try {
      const picksToAdd = Array.from(userPicks).map((key) => {
        const [team, pick, season, nflWeek, userId, leagueId] = key.split('|');
        return {
          id: 0,
          leagueId: Number(leagueId),
          userId,
          userName: user.name ?? '',
          team,
          pick: pick as PickType,
          nflWeek: Number(nflWeek),
          season: Number(season),
          dateCreated: new Date().toISOString(),
        } as NflPickDto;
      });

      if (picksToAdd.length === 0) {
        toast.push('No Picks to Add - Please try again', 'error');
        return;
      }

      await addPicks(picksToAdd);
      toast.push(`${picksToAdd.length} Pick(s) Added`, 'success');
      setUserPicks(new Set());
      await reload();
    } catch {
      toast.push('Error Adding Picks', 'error');
    } finally {
      setStoringPicks(false);
    }
  };

  const getTeamImage = (competition: Competition, isAway: boolean) => {
    const teamAbbr = isAway ? getAwayTeamAbbr(competition) : getHomeTeamAbbr(competition);
    if (showJerseys && jerseyCache[teamAbbr]) return jerseyCache[teamAbbr];
    return isAway ? getAwayTeamLogo(competition) : getHomeTeamLogo(competition);
  };

  if (loading) {
    return (
      <Box>
        <PageHeader title="Picks" />
        <Box sx={{ display: 'flex', justifyContent: 'center', mt: 8 }}>
          <CircularProgress />
        </Box>
      </Box>
    );
  }

  if (!currentLeague) return <NoLeague />;

  if (!hasOdds) return <SpreadRelease />;

  return (
    <Box>
      <PageHeader title="Picks" />
      {scores && (
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
      <Grid container spacing={2}>
        {Object.keys(jerseyCache).length > 0 && (
          <Grid size={12} sx={{ display: 'flex', justifyContent: 'center' }}>
            <Button variant="outlined" color="info" startIcon={<CheckroomIcon />} onClick={() => setShowJerseys((prev) => !prev)}>
              {showJerseys ? 'Show Logos' : 'Show Jerseys'}
            </Button>
          </Grid>
        )}

        {!isPicksLocked() && (
          <Grid size={12}>
            {requiredRemaining > 0 && (
              <Stack spacing={1} alignItems="center">
                <Typography variant="h6">Picks Remaining ({requiredRemaining})</Typography>
                <Typography variant="h6">Submit picks before gametime</Typography>
              </Stack>
            )}
            <Stack direction="row" spacing={2} justifyContent="space-between" sx={{ mt: 2 }}>
              <Button variant="contained" color="success" disabled={isSubmitDisabled()} onClick={handleSubmit}>
                {storingPicks ? 'Submitting…' : 'Submit Pick(s)'}
              </Button>
              <Button variant="contained" color="warning" disabled={isClearPicksDisabled()} onClick={clearPicks}>
                Clear Selected Picks
              </Button>
            </Stack>
          </Grid>
        )}

        {scores?.events?.map((scoreEvent: Event) =>
          scoreEvent.competitions
            .sort((a, b) => getAwayTeamAbbr(a).localeCompare(getAwayTeamAbbr(b)))
            .map((competition) => {
              const awayAbbr = getAwayTeamAbbr(competition);
              const homeAbbr = getHomeTeamAbbr(competition);
              const awaySpreadSelected = isSelected(awayAbbr);
              const homeSpreadSelected = isSelected(homeAbbr);
              const homeOverSelected = isSelected(homeAbbr, 'Over');
              const homeUnderSelected = isSelected(homeAbbr, 'Under');
              const totalOver = getSpread(homeAbbr, 'Over');
              const totalUnder = getSpread(homeAbbr, 'Under');

              return (
                <Grid size={{ xs: 12, lg: 4 }} key={competition.id}>
                  <Paper sx={{ p: 2 }}>
                    <Paper sx={{ p: 2, mb: 2 }}>
                      <Stack direction="row" alignItems="center" justifyContent="space-between">
                        <Box textAlign="center">
                          <img src={getTeamImage(competition, true)} width={50} />
                          {showJerseys && (
                            <Typography variant="caption" fontWeight={700}>
                              {awayAbbr}
                            </Typography>
                          )}
                        </Box>
                        {!isPostSeason && (
                          <Typography variant="subtitle2">{getTeamRecord(getAwayTeam(competition))}</Typography>
                        )}
                        <Typography variant={isPostSeason ? 'subtitle2' : 'h6'} className="fixed-width">
                          {getSpread(awayAbbr) ?? ''}
                        </Typography>
                        {awaySpreadSelected ? (
                          <Button
                            color="success"
                            variant="contained"
                            onClick={() => unselectPick(awayAbbr)}
                            sx={pickButtonSx}
                          >
                            Picked
                          </Button>
                        ) : (
                          <Button
                            color="warning"
                            variant="contained"
                            disabled={isGameStartedOrDisabledPicks(competition)}
                            onClick={() => selectPick(awayAbbr)}
                            sx={pickButtonSx}
                          >
                            Pick
                          </Button>
                        )}
                      </Stack>
                    </Paper>

                    <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ mb: 2, gap: 1 }}>
                      {scoreEvent.weather && (
                        <WeatherIcon
                          iconKey={scoreEvent.weather.displayValue}
                          conditionId={scoreEvent.weather.conditionId}
                          temperatureF={scoreEvent.weather.temperature}
                          showTemp
                        />
                      )}
                      <Typography variant="body2" sx={{ opacity: 0.9 }}>
                        {displayDetails(competition)}
                      </Typography>
                    </Stack>

                    <Paper sx={{ p: 2 }}>
                      <Stack direction="row" alignItems="center" justifyContent="space-between">
                        <Box textAlign="center">
                          <img src={getTeamImage(competition, false)} width={50} />
                          {showJerseys && (
                            <Typography variant="caption" fontWeight={700}>
                              {homeAbbr}
                            </Typography>
                          )}
                        </Box>
                        {!isPostSeason && (
                          <Typography variant="subtitle2">{getTeamRecord(getHomeTeam(competition))}</Typography>
                        )}
                        <Typography variant={isPostSeason ? 'subtitle2' : 'h6'} className="fixed-width">
                          {getSpread(homeAbbr) ?? ''}
                        </Typography>
                        {homeSpreadSelected ? (
                          <Button
                            color="success"
                            variant="contained"
                            onClick={() => unselectPick(homeAbbr)}
                            sx={pickButtonSx}
                          >
                            Picked
                          </Button>
                        ) : (
                          <Button
                            color="warning"
                            variant="contained"
                            disabled={isGameStartedOrDisabledPicks(competition)}
                            onClick={() => selectPick(homeAbbr)}
                            sx={pickButtonSx}
                          >
                            Pick
                          </Button>
                        )}
                      </Stack>
                    </Paper>

                    {isPostSeason && (
                      <Paper
                        data-testid="over-under-controls"
                        sx={{ mt: 1, p: 1.5, borderRadius: 2, background: 'rgba(248, 250, 252, 0.04)' }}
                      >
                        <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ gap: 0.5 }}>
                          <Box textAlign="center" sx={{ minWidth: 44 }}>
                            <Typography variant="caption" sx={{ opacity: 0.7, letterSpacing: 0.5, display: 'block' }}>
                              Over
                            </Typography>
                            <Typography variant="caption" fontWeight={700}>
                              {totalOver ?? '--'}
                            </Typography>
                          </Box>
                          <Button
                            variant="contained"
                            color={homeOverSelected ? 'success' : 'warning'}
                            sx={{ minWidth: 76, height: 36, fontSize: '0.8rem', px: 1 }}
                            disabled={isGameStartedOrDisabledPicks(competition) && !homeOverSelected}
                            onClick={() =>
                              homeOverSelected ? unselectPick(homeAbbr, 'Over') : selectPick(homeAbbr, 'Over')
                            }
                          >
                            {homeOverSelected ? 'Overed' : 'Over'}
                          </Button>
                          <Button
                            variant="contained"
                            color={homeUnderSelected ? 'success' : 'warning'}
                            sx={{ minWidth: 76, height: 36, fontSize: '0.8rem', px: 1 }}
                            disabled={isGameStartedOrDisabledPicks(competition) && !homeUnderSelected}
                            onClick={() =>
                              homeUnderSelected ? unselectPick(homeAbbr, 'Under') : selectPick(homeAbbr, 'Under')
                            }
                          >
                            {homeUnderSelected ? 'Undered' : 'Under'}
                          </Button>
                          <Box textAlign="center" sx={{ minWidth: 44 }}>
                            <Typography variant="caption" sx={{ opacity: 0.7, letterSpacing: 0.5, display: 'block' }}>
                              Under
                            </Typography>
                            <Typography variant="caption" fontWeight={700}>
                              {totalUnder ?? '--'}
                            </Typography>
                          </Box>
                        </Stack>
                      </Paper>
                    )}
                  </Paper>
                </Grid>
              );
            })
        )}
      </Grid>
    </Box>
  );
}
