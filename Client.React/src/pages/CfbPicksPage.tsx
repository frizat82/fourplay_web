import { useCallback, useEffect, useState } from 'react';
import { Box, Button, CircularProgress, Grid, Paper, Stack, Typography } from '@mui/material';
import PageHeader from '../components/PageHeader';
import WeekYearSelector from '../components/WeekYearSelector';
import GameCard, { type PickState } from '../components/sports/GameCard';
import { useSession } from '../services/session';
import { getCfbSlates, getCfbSpreads, getCfbScores, getCfbUserPicks, addCfbPicks, deleteCfbPicks } from '../api/cfb';
import type { CfbSlateDto, CfbSpreadDto, CfbScoreDto, CfbPickDto } from '../types/league';
import { useAuth } from '../services/auth';
import { getCfbWeekName, cfbSlateNumberToWeek, cfbWeekToSlateNumber } from '../utils/gameHelpers';


const SEASON = 2025;
const MIN_SEASON = 2025;
const CFB_REGULAR_WEEKS = Array.from({ length: 14 }, (_, i) => i + 1); // 1-14
const CFB_POST_WEEKS = [1, 2, 3, 4, 5]; // Conf Champs + 4 CFP rounds

function gameIsLocked(gameTime: string): boolean {
  return new Date(gameTime) <= new Date();
}

export default function CfbPicksPage() {
  const { currentLeague, leaguesLoaded } = useSession();
  const { user } = useAuth();

  const [season, setSeason] = useState(SEASON);
  const [week, setWeek] = useState(1);
  const [isPostSeason, setIsPostSeason] = useState(false);

  const [slates, setSlates] = useState<CfbSlateDto[]>([]);
  const [activeSlate, setActiveSlate] = useState<CfbSlateDto | null>(null);
  const [spreads, setSpreads] = useState<CfbSpreadDto[]>([]);
  const [scores, setScores] = useState<CfbScoreDto[]>([]);
  const [existingPicks, setExistingPicks] = useState<Set<string>>(new Set());
  const [userPicks, setUserPicks] = useState<Set<string>>(new Set());
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [isCurrentSlate, setIsCurrentSlate] = useState(true);

  useEffect(() => { if (leaguesLoaded) void initialLoad(); }, [currentLeague, leaguesLoaded]);

  async function initialLoad() {
    setLoading(true);
    setIsCurrentSlate(true);
    try {
      const allSlates = await getCfbSlates(SEASON);
      setSlates(allSlates);
      // Default to most recent active slate
      const now = new Date();
      const active = allSlates.find(s => new Date(s.endDate) >= now) ?? allSlates[allSlates.length - 1] ?? null;
      if (active) {
        const { week: w, isPostSeason: ps } = cfbSlateNumberToWeek(active.slateNumber);
        setWeek(w);
        setIsPostSeason(ps);
        setActiveSlate(active);
        await loadSlateData(active);
      }
    } finally {
      setLoading(false);
    }
  }

  async function loadSlateData(slate: CfbSlateDto) {
    if (!currentLeague) return;
    const [sp, sc, picks] = await Promise.all([
      getCfbSpreads(slate.id),
      getCfbScores(slate.id),
      getCfbUserPicks(currentLeague, slate.id),
    ]);
    setSpreads(sp);
    setScores(sc);
    setExistingPicks(new Set(picks.map(pickKey)));
    setUserPicks(new Set());
  }

  const handleWeekChange = useCallback(async (newWeek: number, meta?: { isPostSeason?: boolean }) => {
    const ps = meta?.isPostSeason ?? isPostSeason;
    setWeek(newWeek);
    setIsPostSeason(ps);
    setIsCurrentSlate(false);
    const slateNum = cfbWeekToSlateNumber(newWeek, ps);
    const slate = slates.find(s => s.slateNumber === slateNum) ?? null;
    setActiveSlate(slate);
    if (slate) await loadSlateData(slate);
  }, [slates, isPostSeason, currentLeague]);

  const handlePostSeasonChange = useCallback(async (ps: boolean) => {
    setIsPostSeason(ps);
    const opts = ps ? CFB_POST_WEEKS : CFB_REGULAR_WEEKS;
    const newWeek = opts[0];
    setWeek(newWeek);
    const slateNum = cfbWeekToSlateNumber(newWeek, ps);
    const slate = slates.find(s => s.slateNumber === slateNum) ?? null;
    setActiveSlate(slate);
    if (slate) await loadSlateData(slate);
  }, [slates, currentLeague]);

  function pickKey(p: CfbPickDto) { return `${p.espnEventId}|${p.team}|${p.pickType}`; }
  function spreadKey(espnEventId: number, team: string) { return `${espnEventId}|${team}|Spread`; }

  function togglePick(espnEventId: number, team: string) {
    const key = spreadKey(espnEventId, team);
    if (existingPicks.has(key)) return;
    setUserPicks(prev => {
      const next = new Set(prev);
      spreads.forEach(sp => {
        if (sp.espnEventId === espnEventId) {
          next.delete(spreadKey(sp.espnEventId, sp.homeTeam));
          next.delete(spreadKey(sp.espnEventId, sp.awayTeam));
        }
      });
      next.add(key);
      return next;
    });
  }

  async function handleSubmit() {
    if (!currentLeague || !activeSlate || userPicks.size === 0) return;
    setSubmitting(true);
    try {
      const picks = [...userPicks].map(k => {
        const [espnEventId, team, pickType] = k.split('|');
        return { espnEventId: Number(espnEventId), team, pickType };
      });
      await addCfbPicks(currentLeague, activeSlate.id, season, picks);
      setUserPicks(new Set());
      if (activeSlate) await loadSlateData(activeSlate);
    } finally {
      setSubmitting(false);
    }
  }

  async function handleClear() {
    if (!currentLeague || !activeSlate) return;
    await deleteCfbPicks(currentLeague, activeSlate.id);
    setUserPicks(new Set());
    if (activeSlate) await loadSlateData(activeSlate);
  }

  if (loading) return <Box sx={{ display: 'flex', justifyContent: 'center', mt: 8 }}><CircularProgress /></Box>;

  const scoreMap = new Map(scores.map(s => [s.espnEventId, s]));
  const totalPicked = existingPicks.size + userPicks.size;

  return (
    <Box>
      <PageHeader title="CFB Picks" subtitle={`Season ${season}`} />

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

      {activeSlate && (
        <>
          <Stack direction="row" justifyContent="space-between" sx={{ mb: 2 }}>
            <Button
              variant="contained"
              color="secondary"
              disabled={userPicks.size === 0 || submitting}
              onClick={handleSubmit}
            >
              Submit {userPicks.size > 0 ? `${userPicks.size} Pick${userPicks.size > 1 ? 's' : ''}` : 'Picks'}
            </Button>
            <Button variant="outlined" color="error" disabled={existingPicks.size === 0} onClick={handleClear}>
              Clear My Picks
            </Button>
          </Stack>

          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            {totalPicked}/{spreads.length} picks submitted
          </Typography>
        </>
      )}

      <Grid container spacing={2}>
        {spreads.map(sp => {
          const score = scoreMap.get(sp.espnEventId);
          const locked = gameIsLocked(sp.gameTime);
          const homeKey = spreadKey(sp.espnEventId, sp.homeTeam);
          const awayKey = spreadKey(sp.espnEventId, sp.awayTeam);
          const homePickState: PickState = existingPicks.has(homeKey) ? 'submitted' : userPicks.has(homeKey) ? 'pending' : 'none';
          const awayPickState: PickState = existingPicks.has(awayKey) ? 'submitted' : userPicks.has(awayKey) ? 'pending' : 'none';

          return (
            <Grid key={sp.espnEventId} size={{ xs: 12, sm: 6, md: 4 }}>
              <GameCard
                homeTeam={sp.homeTeam}
                awayTeam={sp.awayTeam}
                homeSpread={sp.homeTeamSpread}
                awaySpread={sp.awayTeamSpread}
                overUnder={sp.overUnder}
                gameTime={sp.gameTime}
                mode="pick"
                gameStatus={score?.gameStatus}
                homeScore={score?.homeTeamScore}
                awayScore={score?.awayTeamScore}
                homePickState={homePickState}
                awayPickState={awayPickState}
                locked={locked}
                onPickHome={() => togglePick(sp.espnEventId, sp.homeTeam)}
                onPickAway={() => togglePick(sp.espnEventId, sp.awayTeam)}
                weatherDisplayValue={score?.weatherDisplayValue}
                weatherConditionId={score?.weatherConditionId}
                weatherTemperatureF={score?.weatherTemperatureF}
              />
            </Grid>
          );
        })}
      </Grid>

      {spreads.length === 0 && (
        <Paper sx={{ p: 4, textAlign: 'center' }}>
          <Typography color="text.secondary">No games available for this week yet.</Typography>
        </Paper>
      )}
    </Box>
  );
}
