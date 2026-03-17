import { useEffect, useState } from 'react';
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
import ChecklistIcon from '@mui/icons-material/Checklist';
import PageHeader from '../../components/PageHeader';
import { getAllJobsStatus, runMissing, runScores, runSpreads, runUserManager } from '../../api/jobManager';
import type { JobStatusResponse } from '../../types/admin';
import { useToast } from '../../services/toast';

export default function AdminJobManagerPage() {
  const [jobs, setJobs] = useState<JobStatusResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [jobRunning, setJobRunning] = useState(false);
  const toast = useToast();

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
      <PageHeader title="Administrator User Management" />
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
          <Grid size={{ xs: 12, sm: 6 }}>
            <Button
              variant="contained"
              fullWidth
              startIcon={<ChecklistIcon />}
              disabled={jobRunning || loading}
              onClick={() => runJob(runMissing, 'Missing Picks Job')}
            >
              {jobRunning ? 'Running...' : 'Run Missing Picks Job'}
            </Button>
          </Grid>
        </Grid>
      </Paper>

      <Paper sx={{ p: 2 }}>
        <Typography variant="h6" align="center" sx={{ mb: 2 }}>
          Scheduled Jobs
        </Typography>
        {loading ? (
          <Stack alignItems="center">
            <CircularProgress />
          </Stack>
        ) : (
          <Box sx={{ overflowX: 'auto' }}><Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Job Name</TableCell>
                <TableCell>Description</TableCell>
                <TableCell>Status</TableCell>
                <TableCell>Next Run</TableCell>
                <TableCell>Last Run</TableCell>
                <TableCell>Last Started</TableCell>
                <TableCell>Runs</TableCell>
                <TableCell>Last Message</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {jobs.map((job) => (
                <TableRow key={job.jobName}>
                  <TableCell>{job.jobName}</TableCell>
                  <TableCell>{job.description}</TableCell>
                  <TableCell>
                    <Chip size="small" label={job.status} color={getStatusColor(job.status)} />
                  </TableCell>
                  <TableCell>{job.nextRun ? new Date(job.nextRun).toLocaleString() : 'Not scheduled'}</TableCell>
                  <TableCell>{job.lastRun ? new Date(job.lastRun).toLocaleString() : 'Never'}</TableCell>
                  <TableCell>{job.lastStartedUtc ? new Date(job.lastStartedUtc).toLocaleString() : 'Never'}</TableCell>
                  <TableCell>{job.runCount}</TableCell>
                  <TableCell>{job.lastMessage || '—'}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table></Box>
        )}
      </Paper>
    </Box>
  );
}
