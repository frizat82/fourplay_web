import {
  Box,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography,
  useTheme,
} from '@mui/material';
import ArrowCircleUpIcon from '@mui/icons-material/ArrowCircleUp';
import ArrowCircleDownIcon from '@mui/icons-material/ArrowCircleDown';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import CancelIcon from '@mui/icons-material/Cancel';
import type { NflPickDto, SpreadCalculationResponse } from '../types/picks';
import TeamHelmet from './sports/TeamHelmet';

interface UserPicksMatrixProps {
  users: string[];
  picks: NflPickDto[];
  spreads: Record<string, SpreadCalculationResponse>;
  requiredPicks: number;
}

export default function UserPicksMatrix({ users, picks, spreads, requiredPicks }: UserPicksMatrixProps) {
  const isDark = useTheme().palette.mode === 'dark';

  const getWinner = (teamAbbr: string, pickType: NflPickDto['pick']) => {
    const calc = spreads[teamAbbr];
    if (!calc) return null;
    if (pickType === 'Spread') return calc.isWinner;
    if (pickType === 'Over') return calc.isOverWinner;
    return calc.isUnderWinner;
  };

  const renderBadge = (pick: NflPickDto) => {
    const result = getWinner(pick.team, pick.pick);
    // TeamHelmet's dark-mode logos are neon assets designed to glow against a dark background
    // (see TeamHelmet.tsx) — the light literal tones below washed them out in dark mode, so pick
    // each badge's tone from the active theme instead.
    const bgColor = result === true
      ? (isDark ? 'success.dark' : 'success.light')
      : result === false
        ? (isDark ? 'error.dark' : 'error.light')
        : (isDark ? 'grey.800' : 'grey.200');

    return (
      <Paper
        key={`${pick.team}-${pick.pick}`}
        sx={{
          height: 76,
          width: 60,
          borderRadius: 2,
          position: 'relative',
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          justifyContent: 'center',
          backgroundColor: bgColor,
          flexShrink: 0,
        }}
      >
        {pick.pick === 'Over' && (
          <ArrowCircleUpIcon
            fontSize="small"
            sx={{ position: 'absolute', top: 4, left: 4, bgcolor: 'white', borderRadius: '50%' }}
            color={result ? 'success' : 'error'}
          />
        )}
        {pick.pick === 'Under' && (
          <ArrowCircleDownIcon
            fontSize="small"
            sx={{ position: 'absolute', top: 4, left: 4, bgcolor: 'white', borderRadius: '50%' }}
            color={result ? 'success' : 'error'}
          />
        )}
        {/* frizat: the team logo alone was hard to identify at a glance against the win/loss
            color-coded background — show the abbreviation too. */}
        <TeamHelmet abbr={pick.team} size={36} showLabel />
        {result === true && (
          <CheckCircleIcon
            fontSize="small"
            color="success"
            sx={{ position: 'absolute', bottom: 4, right: 4, bgcolor: 'white', borderRadius: '50%' }}
          />
        )}
        {result === false && (
          <CancelIcon
            fontSize="small"
            color="error"
            sx={{ position: 'absolute', bottom: 4, right: 4, bgcolor: 'white', borderRadius: '50%' }}
          />
        )}
      </Paper>
    );
  };

  return (
    <Paper sx={{ overflowX: 'auto' }}>
      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell>User</TableCell>
            {Array.from({ length: requiredPicks }).map((_, idx) => (
              <TableCell key={idx}>Pick {idx + 1}</TableCell>
            ))}
          </TableRow>
        </TableHead>
        <TableBody>
          {users.sort().map((user) => {
            const spreadPicks = picks.filter((p) => p.userName === user && p.pick === 'Spread');
            // frizat: a user can have both a spread pick AND a separate Over/Under pick for the
            // same game in a postseason week (O/U isn't counted toward requiredPicks — it's
            // always a single extra pick tied to that week's one required game). Rather than a
            // dedicated table column, which breaks "columns == requiredPicks" for every other
            // week, the O/U pick renders as a second badge alongside the spread pick in that
            // same required-pick cell.
            const overUnderPicks = picks.filter((p) => p.userName === user && p.pick !== 'Spread');
            return (
              <TableRow key={user}>
                <TableCell>
                  <Typography fontWeight={600}>{user}</Typography>
                </TableCell>
                {Array.from({ length: requiredPicks }).map((_, idx) => {
                  const cellPicks = [spreadPicks[idx], ...(idx === 0 ? overUnderPicks : [])]
                    .filter((p): p is NflPickDto => Boolean(p?.team));
                  return (
                    <TableCell key={idx} align="center">
                      {cellPicks.length === 0 ? (
                        <Paper sx={{ height: 76, width: 60, borderRadius: 2 }} />
                      ) : (
                        <Box sx={{ display: 'flex', gap: 0.5, justifyContent: 'center' }}>
                          {cellPicks.map(renderBadge)}
                        </Box>
                      )}
                    </TableCell>
                  );
                })}
              </TableRow>
            );
          })}
        </TableBody>
      </Table>
    </Paper>
  );
}
