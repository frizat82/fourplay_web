import { Fragment, useEffect, useMemo, useState } from 'react';
import {
  Box,
  Button,
  CircularProgress,
  Grid,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography,
  Chip,
} from '@mui/material';
import TrendingUpIcon from '@mui/icons-material/TrendingUp';
import PeopleIcon from '@mui/icons-material/People';
import ScoreboardIcon from '@mui/icons-material/Scoreboard';
import PageHeader from '../../components/PageHeader';
import { getAllJobsStatus, runScores, runSpreads, runUserManager } from '../../api/jobManager';
import type { JobStatusResponse } from '../../types/admin';
import { useToast } from '../../services/toast';
import { stickyColumnSx } from '../../utils/tableStyles';

export default function AdminJobManagerPage() {
  const [jobs, setJobs] = useState<JobStatusResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [jobRunning, setJobRunning] = useState(false);
  const toast = useToast();

  // Backend already returns jobs ordered by Category then JobName — group in that order rather
  // than re-sorting, so category order stays server-controlled in one place. Every job (fixed
  // scheduler/cron jobs and the per-league/per-week ones TimedTriggerScheduler registers
  // dynamically — Juice Reminder/Lock, NFL/CFB Spreads) is always shown: a "hide dynamic jobs"
  // toggle previously defaulted this list to only fixed jobs, which hid the only place an admin
  // could confirm a league's juice-lock reminder was actually scheduled.
  const jobsByCategory = useMemo(() => {
    const grouped = new Map<string, JobStatusResponse[]>();
    for (const job of jobs) {
      const existing = grouped.get(job.category);
      if (existing) existing.push(job);
      else grouped.set(job.category, [job]);
    }
    return grouped;
  }, [jobs]);

  const loadJobs = async () => {
    setLoading(true);
    try {
      const data = await getAllJobsStatus();
      setJobs(data ?? []);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadJobs();
  }, []);

  const runJob = async (fn: () => Promise<void>, label: string) => {
    try {
      setJobRunning(true);
      await fn();
      toast.push(`Started ${label}`, 'success');
      await loadJobs();
    } catch {
      toast.push(`Error starting ${label}`, 'error');
    } finally {
      setJobRunning(false);
    }
  };

  const getStatusColor = (status: string): 'info' | 'default' =>
    status.toLowerCase() === 'executing' ? 'info' : 'default';

  return (
    <Box>
      <PageHeader title="Job Manager" />
      <Paper sx={{ p: 3, mb: 3 }}>
        <Typography variant="h5" align="center" sx={{ mb: 2 }}>
          Admin Tasks
        </Typography>
        <Grid container spacing={2} justifyContent="center">
          <Grid size={{ xs: 12, sm: 6 }}>
            <Button
              variant="contained"
              fullWidth
              startIcon={<TrendingUpIcon />}
              disabled={jobRunning || loading}
              onClick={() => runJob(runSpreads, 'Spread Job')}
            >
              {jobRunning ? 'Running...' : 'Run Spreads Job'}
            </Button>
          </Grid>
          <Grid size={{ xs: 12, sm: 6 }}>
            <Button
              variant="contained"
              fullWidth
              startIcon={<PeopleIcon />}
              disabled={jobRunning || loading}
              onClick={() => runJob(runUserManager, 'User Manager Job')}
            >
              {jobRunning ? 'Running...' : 'Run User Manager Job'}
            </Button>
          </Grid>
          <Grid size={{ xs: 12, sm: 6 }}>
            <Button
              variant="contained"
              fullWidth
              startIcon={<ScoreboardIcon />}
              disabled={jobRunning || loading}
              onClick={() => runJob(runScores, 'Scores Job')}
            >
              {jobRunning ? 'Running...' : 'Run Scores Job'}
            </Button>
          </Grid>
        </Grid>
      </Paper>

      <Paper sx={{ p: 2 }}>
        <Typography variant="h6" sx={{ mb: 2 }}>Scheduled Jobs</Typography>
        {loading ? (
          <Stack alignItems="center">
            <CircularProgress />
          </Stack>
        ) : (
          <Box sx={{ overflowX: 'auto' }}><Table size="small">
            <TableHead>
              <TableRow>
                <TableCell sx={stickyColumnSx}>Job Name</TableCell>
                <TableCell>League</TableCell>
                <TableCell>Description</TableCell>
                <TableCell>Status</TableCell>
                <TableCell>Next Run</TableCell>
                <TableCell>Last Succeeded</TableCell>
                <TableCell>Last Message</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {[...jobsByCategory.entries()].map(([category, categoryJobs]) => (
                <Fragment key={category}>
                  <TableRow>
                    <TableCell colSpan={7} sx={{ bgcolor: 'action.hover', fontWeight: 600 }}>
                      {category}
                    </TableCell>
                  </TableRow>
                  {categoryJobs.map((job) => (
                    <TableRow key={job.jobName}>
                      <TableCell sx={stickyColumnSx}>{job.jobName}</TableCell>
                      <TableCell>{job.leagueName ?? '—'}</TableCell>
                      <TableCell>{job.description}</TableCell>
                      <TableCell>
                        <Chip size="small" label={job.status} color={getStatusColor(job.status)} />
                      </TableCell>
                      <TableCell>{job.nextRun ? new Date(job.nextRun).toLocaleString() : 'Not scheduled'}</TableCell>
                      <TableCell sx={{ color: job.lastFailedUtc && (!job.lastSucceededUtc || new Date(job.lastFailedUtc) > new Date(job.lastSucceededUtc)) ? 'error.main' : 'inherit' }}>
                        {job.lastSucceededUtc ? new Date(job.lastSucceededUtc).toLocaleString() : 'Never'}
                      </TableCell>
                      <TableCell>{job.lastMessage || '—'}</TableCell>
                    </TableRow>
                  ))}
                </Fragment>
              ))}
              {jobs.length === 0 && (
                <TableRow>
                  <TableCell colSpan={7} align="center">No jobs to show.</TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table></Box>
        )}
      </Paper>
    </Box>
  );
}
