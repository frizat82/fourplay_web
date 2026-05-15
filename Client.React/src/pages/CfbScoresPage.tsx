import { useEffect, useState } from 'react';
import {
  Badge, Box, Button, Chip, CircularProgress, Grid,
  IconButton, Paper, Stack, Typography,
} from '@mui/material';
import PersonIcon from '@mui/icons-material/Person';
import GppGoodIcon from '@mui/icons-material/GppGood';
import GppBadIcon from '@mui/icons-material/GppBad';
import GppMaybeIcon from '@mui/icons-material/GppMaybe';
import ArrowCircleUpIcon from '@mui/icons-material/ArrowCircleUp';
import ArrowCircleDownIcon from '@mui/icons-material/ArrowCircleDown';
import PageHeader from '../components/PageHeader';
import TeamHelmet from '../components/sports/TeamHelmet';
import { useSession } from '../services/session';
import { useAuth } from '../services/auth';
import { getCfbSlates, getCfbSpreads, getCfbScores, getCfbAllPicks } from '../api/cfb';
import type { CfbSlateDto, CfbSpreadDto, CfbScoreDto, CfbPickDto } from '../types/league';

const SEASON = 2025;

function spreadLabel(spread: number): string {
  if (spread === 0) return 'PK';
  return spread > 0 ? `+${spread}` : `${spread}`;
}

function displayStatus(score: CfbScoreDto | undefined, gameTime: string): string {
  if (!score || score.gameStatus === 'StatusScheduled')
    return new Date(gameTime).toLocaleString([], { weekday: 'short', month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' });
  if (score.gameStatus === 'StatusFinal') return 'Final';
  if (score.gameStatus === 'StatusInProgress') return 'Live';
  return '';
}

export default function CfbScoresPage() {
  const { currentLeague } = useSession();
  const { user } = useAuth();

  const [slates, setSlates] = useState<CfbSlateDto[]>([]);
  const [activeSlate, setActiveSlate] = useState<CfbSlateDto | null>(null);
  const [spreads, setSpreads] = useState<CfbSpreadDto[]>([]);
  const [scores, setScores] = useState<CfbScoreDto[]>([]);
  const [allPicks, setAllPicks] = useState<CfbPickDto[]>([]);
  const [showOnlyMyPicks, setShowOnlyMyPicks] = useState(false);
  const [loading, setLoading] = useState(true);

  useEffect(() => { void load(); }, [currentLeague]);

  async function load() {
    setLoading(true);
    try {
      const allSlates = await getCfbSlates(SEASON);
      setSlates(allSlates);
      const active = allSlates.find(s => new Date(s.endDate) >= new Date()) ?? allSlates[allSlates.length - 1] ?? null;
      setActiveSlate(active);
      if (active) await loadSlate(active);
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
    // Determine winner by spread
    const homeCovers = spread
      ? (score.homeTeamScore + spread.homeTeamSpread) > score.awayTeamScore
      : score.homeTeamScore > score.awayTeamScore;
    const teamIsHome = team === score.homeTeam;
    const won = teamIsHome ? homeCovers : !homeCovers;
    return won ? <GppGoodIcon color="success" /> : <GppBadIcon color="error" />;
  }

  function getBadgeColor(espnEventId: number, team: string, score: CfbScoreDto | undefined, spread: CfbSpreadDto | undefined): 'success' | 'error' | 'info' | 'default' {
    if (!score || score.gameStatus !== 'StatusFinal') return 'info';
    const homeCovers = spread
      ? (score.homeTeamScore + spread.homeTeamSpread) > score.awayTeamScore
      : score.homeTeamScore > score.awayTeamScore;
    const teamIsHome = team === score.homeTeam;
    return (teamIsHome ? homeCovers : !homeCovers) ? 'success' : 'error';
  }

  const isCFP = activeSlate?.slateType !== 'RegularSeason';

  if (loading) return <Box sx={{ display: 'flex', justifyContent: 'center', mt: 8 }}><CircularProgress /></Box>;

  return (
    <Box>
      <PageHeader title="CFP Scores" subtitle={`Season ${SEASON}`} />

      <Stack direction="row" spacing={1} sx={{ mb: 2 }} flexWrap="wrap" useFlexGap>
        {slates.map(s => (
          <Chip
            key={s.id}
            label={s.label}
            variant={s.id === activeSlate?.id ? 'filled' : 'outlined'}
            color={s.id === activeSlate?.id ? 'secondary' : 'default'}
            onClick={async () => { setActiveSlate(s); await loadSlate(s); }}
          />
        ))}
      </Stack>

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
                  {/* Score row — mirrors NFL ScoresPage exactly */}
                  <Stack direction="row" alignItems="center" justifyContent="space-between">
                    <TeamHelmet abbr={sp.awayTeam} size={50} />
                    <Typography variant="h6">
                      {score ? score.awayTeamScore : ''}
                    </Typography>
                    <Typography variant="body2" textAlign="center">
                      {displayStatus(score, sp.gameTime)}
                    </Typography>
                    <Typography variant="h6">
                      {score ? score.homeTeamScore : ''}
                    </Typography>
                    <TeamHelmet abbr={sp.homeTeam} size={50} flipped />
                  </Stack>

                  {/* Away team pick row */}
                  <Stack direction="row" alignItems="center" sx={{ mt: 3, gap: 1.5, px: 1 }}>
                    {getPickIcon(sp.espnEventId, sp.awayTeam, score, sp)}
                    <Typography sx={{ minWidth: 40, fontWeight: 600 }}>{sp.awayTeam}</Typography>
                    <Box sx={{ flexGrow: 1, display: 'flex', justifyContent: 'flex-end' }}>
                      <Typography variant="subtitle1">{spreadLabel(sp.awayTeamSpread)}</Typography>
                    </Box>
                    <Badge
                      color={getBadgeColor(sp.espnEventId, sp.awayTeam, score, sp)}
                      overlap="circular"
                      badgeContent={awayPickers}
                      invisible={awayPickers === 0}
                    >
                      <IconButton disabled={awayPickers === 0} size="small">
                        <PersonIcon />
                      </IconButton>
                    </Badge>
                  </Stack>

                  {/* Home team pick row */}
                  <Stack direction="row" alignItems="center" sx={{ mt: 1.5, gap: 1.5, px: 1 }}>
                    {getPickIcon(sp.espnEventId, sp.homeTeam, score, sp)}
                    <Typography sx={{ minWidth: 40, fontWeight: 600 }}>{sp.homeTeam}</Typography>
                    <Box sx={{ flexGrow: 1, display: 'flex', justifyContent: 'flex-end' }}>
                      <Typography variant="subtitle1">{spreadLabel(sp.homeTeamSpread)}</Typography>
                    </Box>
                    <Badge
                      color={getBadgeColor(sp.espnEventId, sp.homeTeam, score, sp)}
                      overlap="circular"
                      badgeContent={homePickers}
                      invisible={homePickers === 0}
                    >
                      <IconButton disabled={homePickers === 0} size="small">
                        <PersonIcon />
                      </IconButton>
                    </Badge>
                  </Stack>

                  {/* O/U row — CFP is always postseason */}
                  {isCFP && (
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
          <Typography color="text.secondary">No games available for this slate.</Typography>
        </Paper>
      )}
    </Box>
  );
}
