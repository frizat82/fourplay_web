import { useEffect, useMemo, useState } from 'react';
import { Navigate } from 'react-router-dom';
import {
  Box,
  Card,
  CardContent,
  CircularProgress,
  Grid,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material';
import PageHeader from '../components/PageHeader';
import { useSession } from '../services/session';
import { useAuth } from '../services/auth';
import { getScores } from '../api/espn';
import { getLeaderboard } from '../api/leaderboard';
import type { LeaderboardDto } from '../types/leaderboard';

export default function LeaderboardPage() {
  const { currentLeague } = useSession();
  const { user } = useAuth();
  const [loading, setLoading] = useState(true);
  const [leaderboard, setLeaderboard] = useState<LeaderboardDto[]>([]);

  const loadScoresWithRetry = async (maxRetries = 5, delayMs = 500) => {
    let attempt = 0;
    let data = null;
    while ((!data?.events || data.events.length === 0) && attempt < maxRetries) {
      data = await getScores();
      if (data?.events && data.events.length > 0) break;
      await new Promise((resolve) => setTimeout(resolve, delayMs));
      attempt += 1;
    }
    return data;
  };

  useEffect(() => {
    const run = async () => {
      setLoading(true);
      if (!currentLeague) {
        setLoading(false);
        return;
      }
      const scores = await loadScoresWithRetry();
      const seasonYear = scores?.season?.year;
      if (!seasonYear) {
        setLoading(false);
        return;
      }
      const data = await getLeaderboard(currentLeague, seasonYear);
      setLeaderboard(data ?? []);
      setLoading(false);
    };
    void run();
  }, [currentLeague]);

  const maxWeek = useMemo(() => {
    if (leaderboard.length === 0) return 0;
    return Math.max(...leaderboard.map((row) => Math.max(...row.weekResults.map((w) => w.week))));
  }, [leaderboard]);

  const getTotalColor = (total: number) => {
    if (total > 0) return 'success.main';
    if (total < 0) return 'error.main';
    return 'text.primary';
  };

  const getWeekSx = (result: string) => {
    switch (result) {
      case 'Won':
        return {
          backgroundColor: 'rgba(22, 163, 74, 0.12)',
          color: 'success.main',
          fontWeight: 500,
        };
      case 'MissingPicks':
        return {
          backgroundColor: 'rgba(230, 126, 34, 0.12)',
          color: 'warning.main',
          fontWeight: 500,
        };
      case 'MissingGameResults':
        return {
          backgroundColor: 'rgba(25, 103, 210, 0.12)',
          color: 'primary.main',
          fontWeight: 500,
        };
      default:
        return {
          backgroundColor: 'rgba(229, 57, 53, 0.12)',
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

  if (!currentLeague) return <Navigate to="/leaguepicker" replace />;

  return (
    <Box>
      <PageHeader title="Leaderboard" />
      {leaderboard.length > 0 && (
        <Grid container spacing={2}>
          <Grid size={12}>
          </Grid>
          <Grid size={12}>
            <Paper sx={{ p: 2 }}>
              <Box sx={{ overflowX: 'auto' }}><Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Rank</TableCell>
                    <TableCell>User</TableCell>
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
                      <TableCell>{row.userName}</TableCell>
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
                  <Card sx={{ backgroundColor: 'rgba(22, 163, 74, 0.12)' }}>
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
                  <Card sx={{ backgroundColor: 'rgba(25, 103, 210, 0.12)' }}>
                    <CardContent sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                      <Box
                        sx={{
                          width: 12,
                          height: 12,
                          borderRadius: 0.5,
                          backgroundColor: 'primary.main',
                        }}
                      />
                      <Typography>Games Incomplete</Typography>
                    </CardContent>
                  </Card>
                </Grid>
                <Grid size={{ xs: 12, sm: 6, md: 3 }}>
                  <Card sx={{ backgroundColor: 'rgba(230, 126, 34, 0.12)' }}>
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
                  <Card sx={{ backgroundColor: 'rgba(229, 57, 53, 0.12)' }}>
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
            </Paper>
          </Grid>
        </Grid>
      )}
    </Box>
  );
}
