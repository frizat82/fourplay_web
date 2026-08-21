import { useEffect, useMemo, useRef, useState } from 'react';
import { Navigate } from 'react-router-dom';
import {
  alpha,
  Box,
  Button,
  Card,
  CardContent,
  CircularProgress,
  Grid,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography,
  useTheme,
} from '@mui/material';
import IosShareIcon from '@mui/icons-material/IosShare';
import PageHeader from '../components/PageHeader';
import ShareableStandingsCard from '../components/ShareableStandingsCard';
import { useSession } from '../services/session';
import { useAuth } from '../services/auth';
import { getLeaderboard } from '../api/leaderboard';
import type { LeaderboardDto } from '../types/leaderboard';
import type { SportAdapter } from '../services/sportAdapter';
import { stickyColumnSx } from '../utils/tableStyles';
import { useShareLink } from '../utils/useShareLink';
import { useShareImage } from '../utils/useShareImage';

interface LeaderboardPageProps {
  adapter: SportAdapter;
}

export default function LeaderboardPage({ adapter }: LeaderboardPageProps) {
  const theme = useTheme();
  const { currentLeague, availableLeagues, leaguesLoaded } = useSession();
  const { user } = useAuth();
  const { share } = useShareLink();
  const { shareImage } = useShareImage();
  const cardRef = useRef<HTMLDivElement>(null);
  const [loading, setLoading] = useState(true);
  const [leaderboard, setLeaderboard] = useState<LeaderboardDto[]>([]);

  useEffect(() => {
    const run = async () => {
      setLoading(true);
      if (!leaguesLoaded || !currentLeague) { setLoading(false); return; }
      const seasonYear = await adapter.currentSeasonYear();
      if (!seasonYear) { setLoading(false); return; }
      const data = await getLeaderboard(currentLeague, seasonYear);
      setLeaderboard(data ?? []);
      setLoading(false);
    };
    void run();
  }, [currentLeague, leaguesLoaded, adapter]);

  const maxWeek = useMemo(() => {
    if (leaderboard.length === 0) return 0;
    return Math.max(...leaderboard.map((row) => Math.max(...row.weekResults.map((w) => w.week))));
  }, [leaderboard]);

  const getTotalColor = (total: number) => {
    if (total > 0) return 'success.main';
    if (total < 0) return 'error.main';
    return 'text.primary';
  };

  // frizat: these used literal hardcoded rgba() constants (e.g. rgba(22, 163, 74, 0.12) for
  // "Won") that don't match theme.ts's actual success/warning/error hex values at all, and stay
  // fixed at the same opacity in both light and dark mode — exactly the anti-pattern the style
  // guide warns about (a literal color at fixed opacity reads fine against one background and
  // washes out against the other). Deriving the tint from the theme's own already-mode-tuned
  // palette color (via alpha()) guarantees the background always matches the text color exactly,
  // in both themes, with one definition instead of four guessed constants. MissingGameResults
  // moved from `primary` (structural navy — very dark in light mode, reads as a near-black smudge
  // at low opacity) to `info` (this app's documented "neutral/informational" semantic), which is
  // what "results not in yet" actually means.
  const getWeekSx = (result: string) => {
    switch (result) {
      case 'Won':
        return {
          backgroundColor: alpha(theme.palette.success.main, 0.16),
          color: 'success.main',
          fontWeight: 500,
        };
      case 'MissingPicks':
        return {
          backgroundColor: alpha(theme.palette.warning.main, 0.16),
          color: 'warning.main',
          fontWeight: 500,
        };
      case 'MissingGameResults':
        return {
          backgroundColor: alpha(theme.palette.info.main, 0.16),
          color: 'info.main',
          fontWeight: 500,
        };
      default:
        return {
          backgroundColor: alpha(theme.palette.error.main, 0.16),
          color: 'error.main',
          fontWeight: 500,
        };
    }
  };

  const rowClass = (row: LeaderboardDto) => {
    if (!user?.name) return {};
    return row.userName === user.name
      ? { backgroundColor: 'action.hover', fontWeight: 600 }
      : {};
  };

  const myRow = leaderboard.find((row) => row.userName === user?.name);
  const leagueName = availableLeagues.find((l) => l.leagueId === currentLeague)?.leagueName ?? 'IV League';

  const handleShare = () => {
    if (!myRow || !cardRef.current) {
      share('IV League Standings', window.location.href);
      return;
    }
    void shareImage(cardRef.current, `${myRow.userName}'s IV League Standings`, 'iv-league-standings.png');
  };

  if (loading) {
    return (
      <Box>
        <PageHeader title="Leaderboard" />
        <Box sx={{ display: 'flex', justifyContent: 'center', mt: 8 }}>
          <CircularProgress />
        </Box>
      </Box>
    );
  }

  if (leaguesLoaded && !currentLeague) return <Navigate to="/leaguepicker" replace />;

  return (
    <Box>
      <PageHeader
        title="Leaderboard"
        action={
          <Button
            size="small"
            variant="outlined"
            startIcon={<IosShareIcon />}
            onClick={handleShare}
          >
            Share
          </Button>
        }
      />
      {myRow && (
        <Box sx={{ position: 'fixed', top: -9999, left: -9999, pointerEvents: 'none' }} aria-hidden="true">
          <ShareableStandingsCard
            ref={cardRef}
            leagueName={leagueName}
            userName={myRow.userName}
            rank={myRow.rank}
            total={myRow.total}
          />
        </Box>
      )}
      {leaderboard.length === 0 && (
        <Typography variant="body2" color="text.secondary" sx={{ mt: 4, textAlign: 'center' }}>
          No leaderboard data yet for this season.
        </Typography>
      )}
      {leaderboard.length > 0 && (
        <Grid container spacing={2}>
          <Grid size={12}>
          </Grid>
          <Grid size={12}>
            <Box>
              <Box sx={{ overflowX: 'auto' }}><Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Rank</TableCell>
                    <TableCell sx={stickyColumnSx}>User</TableCell>
                    <TableCell>Total</TableCell>
                    {Array.from({ length: maxWeek }).map((_, idx) => (
                      <TableCell key={idx}>{`W${maxWeek - idx}`}</TableCell>
                    ))}
                  </TableRow>
                </TableHead>
                <TableBody>
                  {leaderboard.map((row) => (
                    <TableRow key={row.userId} sx={rowClass(row)}>
                      <TableCell>{row.rank}</TableCell>
                      <TableCell sx={stickyColumnSx}>{row.userName}</TableCell>
                      <TableCell sx={{ color: getTotalColor(row.total), fontWeight: 'bold' }}>
                        {row.total}
                      </TableCell>
                      {Array.from({ length: maxWeek }).map((_, idx) => {
                        const weekIndex = maxWeek - idx - 1;
                        const weekValue = row.weekResults[weekIndex];
                        if (!weekValue) {
                          return <TableCell key={idx} />;
                        }
                        return (
                          <TableCell key={idx} sx={getWeekSx(weekValue.weekResult)}>
                            {weekValue.score}
                          </TableCell>
                        );
                      })}
                    </TableRow>
                  ))}
                </TableBody>
              </Table></Box>

              <Grid container spacing={2} justifyContent="center" sx={{ mt: 2 }}>
                <Grid size={{ xs: 12, sm: 6, md: 3 }}>
                  <Card sx={{ backgroundColor: getWeekSx('Won').backgroundColor }}>
                    <CardContent sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                      <Box
                        sx={{
                          width: 12,
                          height: 12,
                          borderRadius: 0.5,
                          backgroundColor: 'success.main',
                        }}
                      />
                      <Typography>Won</Typography>
                    </CardContent>
                  </Card>
                </Grid>
                <Grid size={{ xs: 12, sm: 6, md: 3 }}>
                  <Card sx={{ backgroundColor: getWeekSx('MissingGameResults').backgroundColor }}>
                    <CardContent sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                      <Box
                        sx={{
                          width: 12,
                          height: 12,
                          borderRadius: 0.5,
                          backgroundColor: 'info.main',
                        }}
                      />
                      <Typography>Games Incomplete</Typography>
                    </CardContent>
                  </Card>
                </Grid>
                <Grid size={{ xs: 12, sm: 6, md: 3 }}>
                  <Card sx={{ backgroundColor: getWeekSx('MissingPicks').backgroundColor }}>
                    <CardContent sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                      <Box
                        sx={{
                          width: 12,
                          height: 12,
                          borderRadius: 0.5,
                          backgroundColor: 'warning.main',
                        }}
                      />
                      <Typography>Missing Picks</Typography>
                    </CardContent>
                  </Card>
                </Grid>
                <Grid size={{ xs: 12, sm: 6, md: 3 }}>
                  <Card sx={{ backgroundColor: getWeekSx('Lost').backgroundColor }}>
                    <CardContent sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                      <Box
                        sx={{
                          width: 12,
                          height: 12,
                          borderRadius: 0.5,
                          backgroundColor: 'error.main',
                        }}
                      />
                      <Typography>Lost</Typography>
                    </CardContent>
                  </Card>
                </Grid>
              </Grid>
            </Box>
          </Grid>
        </Grid>
      )}
    </Box>
  );
}
