import {
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
import { stickyColumnSx } from '../utils/tableStyles';

interface UserPicksMatrixProps {
  users: string[];
  picks: NflPickDto[];
  spreads: Record<string, SpreadCalculationResponse>;
  requiredPicks: number;
}

export default function UserPicksMatrix({ users, picks, spreads, requiredPicks }: UserPicksMatrixProps) {
  const isDark = useTheme().palette.mode === 'dark';
  // frizat: a user can have BOTH a spread pick and a separate Over/Under pick in the same
  // postseason week (the O/U pick isn't counted toward requiredPicks) — indexing into a single
  // mixed-type array by position meant whichever pick happened to sort first won that column and
  // the other was silently dropped. Spread picks fill the requiredPicks columns; O/U (if any
  // exist this week) gets its own dedicated column so both are always visible.
  const getSpreadPicks = (user: string) => picks.filter((p) => p.userName === user && p.pick === 'Spread');
  const getOverUnderPick = (user: string) => picks.find((p) => p.userName === user && p.pick !== 'Spread');
  const hasOverUnder = picks.some((p) => p.pick !== 'Spread');

  const getWinner = (teamAbbr: string, pickType: NflPickDto['pick']) => {
    const calc = spreads[teamAbbr];
    if (!calc) return null;
    if (pickType === 'Spread') return calc.isWinner;
    if (pickType === 'Over') return calc.isOverWinner;
    return calc.isUnderWinner;
  };

  const renderCell = (key: string, pick: NflPickDto | undefined) => {
    if (!pick?.team) {
      return (
        <TableCell key={key} align="center">
          <Paper sx={{ height: 76, width: 60, borderRadius: 2 }} />
        </TableCell>
      );
    }
    const result = getWinner(pick.team, pick.pick);
    // TeamHelmet's dark-mode logos are neon assets designed to glow against a dark background
    // (see TeamHelmet.tsx) — the light literal tones below washed them out in dark mode, so pick
    // each cell's tone from the active theme instead.
    const bgColor = result === true
      ? (isDark ? 'success.dark' : 'success.light')
      : result === false
        ? (isDark ? 'error.dark' : 'error.light')
        : (isDark ? 'grey.800' : 'grey.200');

    return (
      <TableCell key={key} align="center">
        <Paper
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
      </TableCell>
    );
  };

  return (
    <Paper sx={{ overflowX: 'auto' }}>
      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell sx={stickyColumnSx}>User</TableCell>
            {Array.from({ length: requiredPicks }).map((_, idx) => (
              <TableCell key={idx}>Pick {idx + 1}</TableCell>
            ))}
            {hasOverUnder && <TableCell>O/U</TableCell>}
          </TableRow>
        </TableHead>
        <TableBody>
          {users.sort().map((user) => {
            const spreadPicks = getSpreadPicks(user);
            return (
              <TableRow key={user}>
                <TableCell sx={stickyColumnSx}>
                  <Typography fontWeight={600}>{user}</Typography>
                </TableCell>
                {Array.from({ length: requiredPicks }).map((_, idx) => renderCell(`${user}-${idx}`, spreadPicks[idx]))}
                {hasOverUnder && renderCell(`${user}-ou`, getOverUnderPick(user))}
              </TableRow>
            );
          })}
        </TableBody>
      </Table>
    </Paper>
  );
}
