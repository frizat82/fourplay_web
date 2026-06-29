import { useEffect, useState } from 'react';
import {
  Box,
  CircularProgress,
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Chip,
  Typography,
} from '@mui/material';
import PageHeader from '../../components/PageHeader';
import { getCfbWeekConfigs } from '../../api/cfb';
import type { CfbSeasonWeekConfigDto } from '../../types/admin';

const CURRENT_SEASON = new Date().getFullYear();
const SEASONS = [CURRENT_SEASON, CURRENT_SEASON - 1];

export default function CfbSchedulePage() {
  const [season, setSeason] = useState(CURRENT_SEASON);
  const [configs, setConfigs] = useState<CfbSeasonWeekConfigDto[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    setLoading(true);
    getCfbWeekConfigs(season)
      .then(setConfigs)
      .finally(() => setLoading(false));
  }, [season]);

  return (
    <Box sx={{ p: { xs: 2, sm: 3 } }}>
      <PageHeader title="CFB Schedule Config" subtitle="ESPN week → IV League slate mapping" />

      <FormControl size="small" sx={{ mb: 3, minWidth: 120 }}>
        <InputLabel>Season</InputLabel>
        <Select value={season} label="Season" onChange={(e) => setSeason(Number(e.target.value))}>
          {SEASONS.map((s) => (
            <MenuItem key={s} value={s}>{s}</MenuItem>
          ))}
        </Select>
      </FormControl>

      {loading ? (
        <CircularProgress />
      ) : (
        <Box sx={{ overflowX: 'auto' }}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>ESPN Week</TableCell>
                <TableCell>IV Slate #</TableCell>
                <TableCell>Week Type</TableCell>
                <TableCell>Scoring Format</TableCell>
                <TableCell>Start Date</TableCell>
                <TableCell>End Date</TableCell>
                <TableCell>In Scope</TableCell>
                <TableCell>Notes</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {configs.length === 0 && (
                <TableRow>
                  <TableCell colSpan={8}>
                    <Typography color="text.secondary" variant="body2">
                      No week configs found for {season}
                    </Typography>
                  </TableCell>
                </TableRow>
              )}
              {configs.map((c) => (
                <TableRow key={c.espnWeekNumber}>
                  <TableCell>{c.espnWeekNumber}</TableCell>
                  <TableCell>{c.ivLeagueWeekNumber}</TableCell>
                  <TableCell>{c.weekType}</TableCell>
                  <TableCell>{c.scoringFormat}</TableCell>
                  <TableCell>{c.weekStartDate}</TableCell>
                  <TableCell>{c.weekEndDate}</TableCell>
                  <TableCell>
                    <Chip
                      label={c.inScopeIvLeague ? 'Yes' : 'No'}
                      color={c.inScopeIvLeague ? 'success' : 'default'}
                      size="small"
                    />
                  </TableCell>
                  <TableCell>{c.notes ?? '—'}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Box>
      )}
    </Box>
  );
}
