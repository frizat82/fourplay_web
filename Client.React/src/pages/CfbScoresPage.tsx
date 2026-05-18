import { useCallback, useEffect, useState } from 'react';
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
import TeamHelmet from '../components/sports/TeamHelmet';
import { useSession } from '../services/session';
import { useAuth } from '../services/auth';
import { getCfbSlates, getCfbSpreads, getCfbScores, getCfbAllPicks } from '../api/cfb';
import type { CfbSlateDto, CfbSpreadDto, CfbScoreDto, CfbPickDto } from '../types/league';
import { cfbSlateNumberToWeek, cfbWeekToSlateNumber, getCfbWeekName, spreadLabel } from '../utils/gameHelpers';

const SEASON = 2025;
const MIN_SEASON = 2025;
const CFB_REGULAR_WEEKS = Array.from({ length: 14 }, (_, i) => i + 1);
const CFB_POST_WEEKS = [1, 2, 3, 4, 5];

function displayStatus(score: CfbScoreDto | undefined, gameTime: string): string {
  if (!score || score.gameStatus === 'StatusScheduled')
    return new Date(gameTime).toLocaleString([], { weekday: 'short', month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' });
  if (score.gameStatus === 'StatusFinal') return 'Final';
  if (score.gameStatus === 'StatusInProgress') return 'Live';
  return '';
}

export default function CfbScoresPage() {
  const { currentLeague, leaguesLoaded } = useSession();
  const { user } = useAuth();

  const [season, setSeason] = useState(SEASON);
  const [week, setWeek] = useState(1);
  const [isPostSeason, setIsPostSeason] = useState(false);

  const [slates, setSlates] = useState<CfbSlateDto[]>([]);
  const [activeSlate, setActiveSlate] = useState<CfbSlateDto | null>(null);
  const [spreads, setSpreads] = useState<CfbSpreadDto[]>([]);
  const [scores, setScores] = useState<CfbScoreDto[]>([]);
  const [allPicks, setAllPicks] = useState<CfbPickDto[]>([]);
  const [showOnlyMyPicks, setShowOnlyMyPicks] = useState(false);
  const [loading, setLoading] = useState(true);
  const [isCurrentSlate, setIsCurrentSlate] = useState(true);

  useEffect(() => { if (leaguesLoaded) void initialLoad(); }, [currentLeague, leaguesLoaded]);

  async function initialLoad() {
    setLoading(true);
    setIsCurrentSlate(true);
    try {
      const allSlates = await getCfbSlates(SEASON);
      setSlates(allSlates);
      const now = new Date();
      const active = allSlates.find(s => new Date(s.endDate) >= now) ?? allSlates[allSlates.length - 1] ?? null;
      if (active) {
        const { week: w, isPostSeason: ps } = cfbSlateNumberToWeek(active.slateNumber);
        setWeek(w);
        setIsPostSeason(ps);
        setActiveSlate(active);
        await loadSlate(active);
      }
    } finally {
      setLoading(false);
    }
  }

  async function loadSlate(slate: CfbSlateDto) {
    const [sp, sc, picks] = await Promise.all([
      getCfbSpreads(slate.id),
      getCfbScores(slate.id),
      currentLeague ? getCfbAllPicks(currentLeague, slate.id) : Promise.resolve([]),
    ]);
    setSpreads(sp);
    setScores(sc);
    setAllPicks(picks);
  }

  const handleWeekChange = useCallback(async (newWeek: number, meta?: { isPostSeason?: boolean }) => {
    const ps = meta?.isPostSeason ?? isPostSeason;
    setWeek(newWeek);
    setIsPostSeason(ps);
    setIsCurrentSlate(false);
    const slateNum = cfbWeekToSlateNumber(newWeek, ps);
    const slate = slates.find(s => s.slateNumber === slateNum) ?? null;
    setActiveSlate(slate);
    if (slate) await loadSlate(slate);
  }, [slates, isPostSeason, currentLeague]);

  const handlePostSeasonChange = useCallback(async (ps: boolean) => {
    setIsPostSeason(ps);
    const opts = ps ? CFB_POST_WEEKS : CFB_REGULAR_WEEKS;
    const newWeek = opts[0];
    setWeek(newWeek);
    const slateNum = cfbWeekToSlateNumber(newWeek, ps);
    const slate = slates.find(s => s.slateNumber === slateNum) ?? null;
    setActiveSlate(slate);
    if (slate) await loadSlate(slate);
  }, [slates, currentLeague]);

  const scoreMap = new Map(scores.map(s => [s.espnEventId, s]));
  const spreadMap = new Map(spreads.map(s => [s.espnEventId, s]));

  function getPickCount(espnEventId: number, team: string) {
    return allPicks.filter(p => p.espnEventId === espnEventId && p.team === team).length;
  }

  function myPick(espnEventId: number): string | undefined {
    return allPicks.find(p => p.espnEventId === espnEventId && p.userId === user?.userId)?.team;
  }

  function getPickIcon(espnEventId: number, team: string, score: CfbScoreDto | undefined, spread: CfbSpreadDto | undefined) {
    const picked = myPick(espnEventId) === team;
    if (!picked) return <GppMaybeIcon color="disabled" />;
    if (!score || score.gameStatus !== 'StatusFinal') return <GppMaybeIcon color="info" />;
    const homeCovers = spread
      ? (score.homeTeamScore + spread.homeTeamSpread) > score.awayTeamScore
      : score.homeTeamScore > score.awayTeamScore;
    const won = team === score.homeTeam ? homeCovers : !homeCovers;
    return won ? <GppGoodIcon color="success" /> : <GppBadIcon color="error" />;
  }

  function getBadgeColor(espnEventId: number, team: string, score: CfbScoreDto | undefined, spread: CfbSpreadDto | undefined): 'success' | 'error' | 'info' | 'default' {
    if (!score || score.gameStatus !== 'StatusFinal') return 'info';
    const homeCovers = spread
      ? (score.homeTeamScore + spread.homeTeamSpread) > score.awayTeamScore
      : score.homeTeamScore > score.awayTeamScore;
    return (team === score.homeTeam ? homeCovers : !homeCovers) ? 'success' : 'error';
  }

  const isPostSeasonSlate = activeSlate?.slateType !== 'RegularSeason' && activeSlate?.slateType !== 'ConferenceChampionship';

  if (loading) return <Box sx={{ display: 'flex', justifyContent: 'center', mt: 8 }}><CircularProgress /></Box>;

  return (
    <Box>
      <PageHeader title="CFB Scores" subtitle={`Season ${season}`} />

      <WeekYearSelector
        season={season}
        week={week}
        isPostSeason={isPostSeason}
        onSeasonChange={setSeason}
        onWeekChange={handleWeekChange}
        onSeasonTypeChange={handlePostSeasonChange}
        regularWeekOptions={CFB_REGULAR_WEEKS}
        postSeasonWeekOptions={CFB_POST_WEEKS}
        minSeason={MIN_SEASON}
        maxSeason={new Date().getFullYear()}
        maxRegularSeasonWeek={14}
        weekLabelFn={getCfbWeekName}
      />
      {!isCurrentSlate && (
        <Box sx={{ display: 'flex', justifyContent: 'center', mt: -1, mb: 2 }}>
          <Button size="small" variant="outlined" onClick={() => void initialLoad()}>
            Current Week
          </Button>
        </Box>
      )}

      <Stack direction="row" spacing={2} sx={{ mb: 3 }}>
        <Button
          variant={showOnlyMyPicks ? 'contained' : 'outlined'}
          color="secondary"
          size="small"
          onClick={() => setShowOnlyMyPicks(p => !p)}
        >
          {showOnlyMyPicks ? 'Show All Games' : 'Show Only My Picks'}
        </Button>
      </Stack>

      <Grid container spacing={2}>
        {spreads
          .filter(sp => !showOnlyMyPicks || myPick(sp.espnEventId) !== undefined)
          .map(sp => {
            const score = scoreMap.get(sp.espnEventId);
            const homePickers = getPickCount(sp.espnEventId, sp.homeTeam);
            const awayPickers = getPickCount(sp.espnEventId, sp.awayTeam);

            return (
              <Grid key={sp.espnEventId} size={{ xs: 12, md: 6, lg: 4 }}>
                <Paper sx={{ p: 2, pt: 2.5 }}>
                  <Stack direction="row" alignItems="center" justifyContent="space-between">
                    <TeamHelmet abbr={sp.awayTeam} size={50} />
                    <Typography variant="h6">{score ? score.awayTeamScore : ''}</Typography>
                    <Typography variant="body2" textAlign="center">{displayStatus(score, sp.gameTime)}</Typography>
                    <Typography variant="h6">{score ? score.homeTeamScore : ''}</Typography>
                    <TeamHelmet abbr={sp.homeTeam} size={50} flipped />
                  </Stack>

                  <Stack direction="row" alignItems="center" sx={{ mt: 3, gap: 1.5, px: 1 }}>
                    {getPickIcon(sp.espnEventId, sp.awayTeam, score, spreadMap.get(sp.espnEventId))}
                    <Typography sx={{ minWidth: 40, fontWeight: 600 }}>{sp.awayTeam}</Typography>
                    <Box sx={{ flexGrow: 1, display: 'flex', justifyContent: 'flex-end' }}>
                      <Typography variant="subtitle1">{spreadLabel(sp.awayTeamSpread)}</Typography>
                    </Box>
                    <Badge color={getBadgeColor(sp.espnEventId, sp.awayTeam, score, spreadMap.get(sp.espnEventId))} overlap="circular" badgeContent={awayPickers} invisible={awayPickers === 0}>
                      <IconButton disabled={awayPickers === 0} size="small"><PersonIcon /></IconButton>
                    </Badge>
                  </Stack>

                  <Stack direction="row" alignItems="center" sx={{ mt: 1.5, gap: 1.5, px: 1 }}>
                    {getPickIcon(sp.espnEventId, sp.homeTeam, score, spreadMap.get(sp.espnEventId))}
                    <Typography sx={{ minWidth: 40, fontWeight: 600 }}>{sp.homeTeam}</Typography>
                    <Box sx={{ flexGrow: 1, display: 'flex', justifyContent: 'flex-end' }}>
                      <Typography variant="subtitle1">{spreadLabel(sp.homeTeamSpread)}</Typography>
                    </Box>
                    <Badge color={getBadgeColor(sp.espnEventId, sp.homeTeam, score, spreadMap.get(sp.espnEventId))} overlap="circular" badgeContent={homePickers} invisible={homePickers === 0}>
                      <IconButton disabled={homePickers === 0} size="small"><PersonIcon /></IconButton>
                    </Badge>
                  </Stack>

                  {isPostSeasonSlate && (
                    <Stack direction="row" alignItems="center" justifyContent="center" sx={{ mt: 2.5, gap: 2 }}>
                      <ArrowCircleUpIcon color="disabled" fontSize="small" />
                      <Typography variant="subtitle1">{sp.overUnder}</Typography>
                      <Typography variant="caption" color="text.secondary">O/U</Typography>
                      <ArrowCircleDownIcon color="disabled" fontSize="small" />
                    </Stack>
                  )}
                </Paper>
              </Grid>
            );
          })}
      </Grid>

      {spreads.length === 0 && !loading && (
        <Paper sx={{ p: 4, textAlign: 'center' }}>
          <Typography color="text.secondary">No games available for this week.</Typography>
        </Paper>
      )}
    </Box>
  );
}
