import { useEffect, useState } from 'react';
import {
  Box, Button, Card, CardContent, Chip, CircularProgress,
  Stack, Typography, Paper,
} from '@mui/material';
import PageHeader from '../components/PageHeader';
import { useSession } from '../services/session';
import { getCfbSlates, getCfbSpreads, getCfbScores, getCfbUserPicks, addCfbPicks, deleteCfbPicks } from '../api/cfb';
import type { CfbSlateDto, CfbSpreadDto, CfbScoreDto, CfbPickDto } from '../types/league';
import { useAuth } from '../services/auth';

const SEASON = 2025;

function gameIsLocked(gameTime: string): boolean {
  return new Date(gameTime) <= new Date();
}

function statusLabel(score: CfbScoreDto | undefined, gameTime: string): string {
  if (!score) return new Date(gameTime).toLocaleString();
  if (score.gameStatus === 'StatusFinal') return 'Final';
  if (score.gameStatus === 'StatusInProgress') return 'In Progress';
  return new Date(gameTime).toLocaleString();
}

export default function CfbPicksPage() {
  const { currentLeague } = useSession();
  const { user } = useAuth();

  const [slates, setSlates] = useState<CfbSlateDto[]>([]);
  const [activeSlate, setActiveSlate] = useState<CfbSlateDto | null>(null);
  const [spreads, setSpreads] = useState<CfbSpreadDto[]>([]);
  const [scores, setScores] = useState<CfbScoreDto[]>([]);
  const [existingPicks, setExistingPicks] = useState<Set<string>>(new Set());
  const [userPicks, setUserPicks] = useState<Set<string>>(new Set());
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [currentLeague]);

  async function load() {
    setLoading(true);
    try {
      const allSlates = await getCfbSlates(SEASON);
      setSlates(allSlates);

      // Show the earliest slate that isn't fully completed, or last slate if all done
      const active = allSlates.find(s => new Date(s.endDate) >= new Date()) ?? allSlates[allSlates.length - 1] ?? null;
      setActiveSlate(active);

      if (active && currentLeague) {
        const [sp, sc, picks] = await Promise.all([
          getCfbSpreads(active.id),
          getCfbScores(active.id),
          getCfbUserPicks(currentLeague, active.id),
        ]);
        setSpreads(sp);
        setScores(sc);
        setExistingPicks(new Set(picks.map(p => pickKey(p))));
      }
    } finally {
      setLoading(false);
    }
  }

  function pickKey(p: CfbPickDto) { return `${p.espnEventId}|${p.team}|${p.pickType}`; }
  function spreadKey(espnEventId: number, team: string) { return `${espnEventId}|${team}|Spread`; }

  function togglePick(espnEventId: number, team: string) {
    const key = spreadKey(espnEventId, team);
    if (existingPicks.has(key)) return; // already submitted
    setUserPicks(prev => {
      const next = new Set(prev);
      // Remove any existing pick for this event
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
      await addCfbPicks(currentLeague, activeSlate.id, SEASON, picks);
      setUserPicks(new Set());
      await load();
    } finally {
      setSubmitting(false);
    }
  }

  async function handleClear() {
    if (!currentLeague || !activeSlate) return;
    await deleteCfbPicks(currentLeague, activeSlate.id);
    setUserPicks(new Set());
    await load();
  }

  if (loading) return <Box sx={{ display: 'flex', justifyContent: 'center', mt: 8 }}><CircularProgress /></Box>;
  if (!activeSlate) return <Box p={3}><Typography>No CFP slate data available.</Typography></Box>;

  const scoreMap = new Map(scores.map(s => [s.espnEventId, s]));
  const totalPicked = existingPicks.size + userPicks.size;
  const requiredPicks = spreads.length; // pick every game in the slate

  return (
    <Box>
      <PageHeader
        title={activeSlate.label}
        subtitle={`${activeSlate.startDate} – ${activeSlate.endDate} · Season ${SEASON}`}
      />

      <Stack direction="row" spacing={1} sx={{ mb: 2 }}>
        {slates.map(s => (
          <Chip
            key={s.id}
            label={s.label}
            variant={s.id === activeSlate.id ? 'filled' : 'outlined'}
            color={s.id === activeSlate.id ? 'secondary' : 'default'}
            onClick={() => {
              setActiveSlate(s);
              if (currentLeague) {
                void Promise.all([
                  getCfbSpreads(s.id).then(setSpreads),
                  getCfbScores(s.id).then(setScores),
                  getCfbUserPicks(currentLeague, s.id).then(p => setExistingPicks(new Set(p.map(pickKey)))),
                ]);
              }
            }}
          />
        ))}
      </Stack>

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
        {totalPicked}/{requiredPicks} picks submitted
      </Typography>

      <Stack spacing={2}>
        {spreads.map(sp => {
          const score = scoreMap.get(sp.espnEventId);
          const locked = gameIsLocked(sp.gameTime);
          const homeKey = spreadKey(sp.espnEventId, sp.homeTeam);
          const awayKey = spreadKey(sp.espnEventId, sp.awayTeam);
          const homeSelected = existingPicks.has(homeKey) || userPicks.has(homeKey);
          const awaySelected = existingPicks.has(awayKey) || userPicks.has(awayKey);
          const homeSubmitted = existingPicks.has(homeKey);
          const awaySubmitted = existingPicks.has(awayKey);

          return (
            <Card key={sp.espnEventId} elevation={2}>
              <CardContent>
                <Typography variant="caption" color="text.secondary">
                  {statusLabel(score, sp.gameTime)}
                </Typography>
                {score && score.gameStatus === 'StatusFinal' && (
                  <Typography variant="h6" fontWeight={700} sx={{ mb: 1 }}>
                    {score.homeTeam} {score.homeTeamScore} – {score.awayTeamScore} {score.awayTeam}
                  </Typography>
                )}
                <Stack direction="row" spacing={2} alignItems="center" sx={{ mt: 1 }}>
                  <Button
                    fullWidth
                    variant={homeSelected ? 'contained' : 'outlined'}
                    color={homeSubmitted ? 'success' : 'secondary'}
                    disabled={locked && !homeSelected}
                    onClick={() => togglePick(sp.espnEventId, sp.homeTeam)}
                  >
                    {sp.homeTeam} {sp.homeTeamSpread > 0 ? `+${sp.homeTeamSpread}` : sp.homeTeamSpread}
                  </Button>
                  <Typography variant="body2" color="text.secondary" sx={{ minWidth: 40, textAlign: 'center' }}>
                    O/U {sp.overUnder}
                  </Typography>
                  <Button
                    fullWidth
                    variant={awaySelected ? 'contained' : 'outlined'}
                    color={awaySubmitted ? 'success' : 'secondary'}
                    disabled={locked && !awaySelected}
                    onClick={() => togglePick(sp.espnEventId, sp.awayTeam)}
                  >
                    {sp.awayTeam} {sp.awayTeamSpread > 0 ? `+${sp.awayTeamSpread}` : sp.awayTeamSpread}
                  </Button>
                </Stack>
              </CardContent>
            </Card>
          );
        })}
      </Stack>

      {spreads.length === 0 && (
        <Paper sx={{ p: 4, textAlign: 'center' }}>
          <Typography color="text.secondary">No games available for this slate yet.</Typography>
        </Paper>
      )}
    </Box>
  );
}
