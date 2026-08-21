import { useEffect, useMemo, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  FormControl,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material';
import PageHeader from '../../components/PageHeader';
import { getAllLeaguesCost } from '../../api/league';
import type { AdminLeagueCostDto } from '../../types/admin';

const SEASON_OPTIONS_BACK = 2;
const SEASON_OPTIONS_FORWARD = 1;

export default function AdminLeagueCostsPage() {
  const currentYear = new Date().getFullYear();
  const seasonOptions = useMemo(
    () => Array.from(
      { length: SEASON_OPTIONS_BACK + SEASON_OPTIONS_FORWARD + 1 },
      (_, i) => currentYear + SEASON_OPTIONS_FORWARD - i
    ),
    [currentYear]
  );

  const [season, setSeason] = useState(currentYear);
  const [costs, setCosts] = useState<AdminLeagueCostDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);

  const load = async () => {
    setLoading(true);
    setError(false);
    try {
      const data = await getAllLeaguesCost(season);
      setCosts(data);
    } catch {
      setError(true);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [season]);

  const total = costs.reduce((sum, c) => sum + c.cost, 0);

  return (
    <Box>
      <PageHeader title="League Costs" />

      <FormControl sx={{ minWidth: 140, mb: 3 }}>
        <InputLabel>Season</InputLabel>
        <Select
          value={season}
          label="Season"
          onChange={(e) => setSeason(Number(e.target.value))}
        >
          {seasonOptions.map((year) => (
            <MenuItem key={year} value={year}>{year}</MenuItem>
          ))}
        </Select>
      </FormControl>

      {loading && (
        <Stack alignItems="center" sx={{ mt: 4 }}>
          <CircularProgress />
        </Stack>
      )}

      {!loading && error && (
        <Alert
          severity="error"
          action={<Button color="inherit" size="small" onClick={() => void load()}>Retry</Button>}
        >
          Couldn&apos;t load league costs. Check your connection and try again.
        </Alert>
      )}

      {!loading && !error && costs.length === 0 && (
        <Typography color="text.secondary" sx={{ textAlign: 'center', mt: 4 }}>
          No leagues found for {season}.
        </Typography>
      )}

      {!loading && !error && costs.length > 0 && (
        <Paper sx={{ p: 2 }}>
          <Box sx={{ overflowX: 'auto' }}>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>League</TableCell>
                  <TableCell>Sport</TableCell>
                  <TableCell>Owner</TableCell>
                  <TableCell>Members</TableCell>
                  <TableCell>Cost</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {costs.map((c) => (
                  <TableRow key={c.leagueId}>
                    <TableCell>{c.leagueName}</TableCell>
                    <TableCell>{c.leagueType.toUpperCase()}</TableCell>
                    <TableCell>{c.ownerUserName}</TableCell>
                    <TableCell>{c.memberCount}</TableCell>
                    <TableCell>${c.cost}</TableCell>
                  </TableRow>
                ))}
                <TableRow>
                  <TableCell colSpan={4} sx={{ fontWeight: 700 }}>Total</TableCell>
                  <TableCell sx={{ fontWeight: 700 }}>${total}</TableCell>
                </TableRow>
              </TableBody>
            </Table>
          </Box>
        </Paper>
      )}
    </Box>
  );
}
