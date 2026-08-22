import { useState } from 'react';
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
import { useQuery } from '@tanstack/react-query';
import PageHeader from '../../components/PageHeader';
import { getAllLeaguesCost } from '../../api/league';

const CURRENT_YEAR = new Date().getFullYear();
const SEASON_OPTIONS = [CURRENT_YEAR + 1, CURRENT_YEAR, CURRENT_YEAR - 1, CURRENT_YEAR - 2];

export default function AdminLeagueCostsPage() {
  const [season, setSeason] = useState(CURRENT_YEAR);

  const { data: costs, isLoading, isError, refetch } = useQuery({
    queryKey: ['all-leagues-cost', season],
    queryFn: () => getAllLeaguesCost(season),
  });

  const total = (costs ?? []).reduce((sum, c) => sum + c.cost, 0);

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
          {SEASON_OPTIONS.map((year) => (
            <MenuItem key={year} value={year}>{year}</MenuItem>
          ))}
        </Select>
      </FormControl>

      {isLoading && (
        <Stack alignItems="center" sx={{ mt: 4 }}>
          <CircularProgress />
        </Stack>
      )}

      {!isLoading && isError && (
        <Alert
          severity="error"
          action={<Button color="inherit" size="small" onClick={() => void refetch()}>Retry</Button>}
        >
          Couldn&apos;t load league costs. Check your connection and try again.
        </Alert>
      )}

      {!isLoading && !isError && costs?.length === 0 && (
        <Typography color="text.secondary" sx={{ textAlign: 'center', mt: 4 }}>
          No leagues found for {season}.
        </Typography>
      )}

      {!isLoading && !isError && costs && costs.length > 0 && (
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
