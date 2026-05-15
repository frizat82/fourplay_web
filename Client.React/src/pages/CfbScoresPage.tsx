import { useEffect, useState } from 'react';
import { Box, Chip, CircularProgress, Grid, Paper, Stack, Typography } from '@mui/material';
import PageHeader from '../components/PageHeader';
import GameCard from '../components/sports/GameCard';
import { useSession } from '../services/session';
import { getCfbSlates, getCfbSpreads, getCfbScores, getCfbAllPicks } from '../api/cfb';
import type { CfbSlateDto, CfbSpreadDto, CfbScoreDto, CfbPickDto } from '../types/league';
import { useAuth } from '../services/auth';

const SEASON = 2025;

export default function CfbScoresPage() {
  const { currentLeague } = useSession();
  const { user } = useAuth();

  const [slates, setSlates] = useState<CfbSlateDto[]>([]);
  const [activeSlate, setActiveSlate] = useState<CfbSlateDto | null>(null);
  const [spreads, setSpreads] = useState<CfbSpreadDto[]>([]);
  const [scores, setScores] = useState<CfbScoreDto[]>([]);
  const [allPicks, setAllPicks] = useState<CfbPickDto[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => { void load(); }, [currentLeague]);

  async function load() {
    setLoading(true);
    try {
      const allSlates = await getCfbSlates(SEASON);
      setSlates(allSlates);
      const active = allSlates.find(s => new Date(s.endDate) >= new Date()) ?? allSlates[allSlates.length - 1] ?? null;
      setActiveSlate(active);
      if (active) await loadSlate(active, currentLeague);
    } finally {
      setLoading(false);
    }
  }

  async function loadSlate(slate: CfbSlateDto, leagueId: number | null) {
    const [sp, sc, picks] = await Promise.all([
      getCfbSpreads(slate.id),
      getCfbScores(slate.id),
      leagueId ? getCfbAllPicks(leagueId, slate.id) : Promise.resolve([]),
    ]);
    setSpreads(sp);
    setScores(sc);
    setAllPicks(picks);
  }

  if (loading) return <Box sx={{ display: 'flex', justifyContent: 'center', mt: 8 }}><CircularProgress /></Box>;
  if (!activeSlate) return <Box p={3}><Typography>No CFP score data available.</Typography></Box>;

  const scoreMap = new Map(scores.map(s => [s.espnEventId, s]));

  return (
    <Box>
      <PageHeader title="CFP Scores" subtitle={`Season ${SEASON}`} />

      <Stack direction="row" spacing={1} sx={{ mb: 3 }} flexWrap="wrap">
        {slates.map(s => (
          <Chip
            key={s.id}
            label={s.label}
            variant={s.id === activeSlate.id ? 'filled' : 'outlined'}
            color={s.id === activeSlate.id ? 'secondary' : 'default'}
            onClick={async () => {
              setActiveSlate(s);
              await loadSlate(s, currentLeague);
            }}
          />
        ))}
      </Stack>

      {spreads.length === 0 ? (
        <Paper sx={{ p: 4, textAlign: 'center' }}>
          <Typography color="text.secondary">No games available for this slate.</Typography>
        </Paper>
      ) : (
        <Grid container spacing={2}>
          {spreads.map(sp => {
            const score = scoreMap.get(sp.espnEventId);
            const homePickers = allPicks.filter(p => p.espnEventId === sp.espnEventId && p.team === sp.homeTeam).length;
            const awayPickers = allPicks.filter(p => p.espnEventId === sp.espnEventId && p.team === sp.awayTeam).length;

            return (
              <Grid key={sp.espnEventId} size={{ xs: 12, sm: 6, md: 4 }}>
                <GameCard
                  homeTeam={sp.homeTeam}
                  awayTeam={sp.awayTeam}
                  homeSpread={sp.homeTeamSpread}
                  awaySpread={sp.awayTeamSpread}
                  overUnder={sp.overUnder}
                  gameTime={sp.gameTime}
                  mode="score"
                  gameStatus={score?.gameStatus}
                  homeScore={score?.homeTeamScore}
                  awayScore={score?.awayTeamScore}
                  homePickers={homePickers}
                  awayPickers={awayPickers}
                />
              </Grid>
            );
          })}
        </Grid>
      )}
    </Box>
  );
}
