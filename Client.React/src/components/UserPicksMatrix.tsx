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
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import CancelIcon from '@mui/icons-material/Cancel';
import type { NflPickDto, SpreadCalculationResponse } from '../types/picks';
import { stickyColumnSx } from '../utils/tableStyles';

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
        {/* frizat: the helmet logo + 9px label was hard to read at a glance against the
            win/loss color-coded background — text-only, much larger, reads clearly instead.
            CFB abbreviations run up to 4 chars (UTSA, UNLV, WASH) vs NFL's 2-3 — shrink to fit
            the fixed 60px badge width rather than overflowing it. */}
        <Typography sx={{ fontSize: pick.team.length > 3 ? 16 : 22, fontWeight: 800, letterSpacing: '0.02em' }}>
          {pick.team}
        </Typography>
        {/* frizat: a tiny corner arrow icon was easy to miss against the color-coded background
            and the large team text above it — spell out OVER/UNDER instead, same bold treatment.
            Match the badge background's 3-way branch (win/loss/no-result-yet) — result is null
            for a scheduled/in-progress game (ScoresPage's matrixSpreads only populates an entry
            once a game is final), which must read as neutral, not silently collapse into "loss". */}
        {pick.pick !== 'Spread' && (
          <Typography
            sx={{
              fontSize: 11,
              fontWeight: 800,
              letterSpacing: '0.03em',
              color: result === true ? 'success.main' : result === false ? 'error.main' : 'text.secondary',
            }}
          >
            {pick.pick.toUpperCase()}
          </Typography>
        )}
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
            <TableCell sx={stickyColumnSx}>User</TableCell>
            {Array.from({ length: requiredPicks }).map((_, idx) => (
              <TableCell key={idx}>Pick {idx + 1}</TableCell>
            ))}
          </TableRow>
        </TableHead>
        <TableBody>
          {users.sort().map((user) => {
            // frizat: Over/Under is an alternate pick TYPE for one game, not an additional pick —
            // AddPicks' server-side validation caps total picks (any type) at requiredPicks, so a
            // real user's picks for this user/week always number exactly requiredPicks regardless
            // of type mix. One badge per required-pick column, no type filtering needed.
            const userPicks = picks.filter((p) => p.userName === user);
            return (
              <TableRow key={user}>
                <TableCell sx={stickyColumnSx}>
                  <Typography fontWeight={600}>{user}</Typography>
                </TableCell>
                {Array.from({ length: requiredPicks }).map((_, idx) => {
                  const pick = userPicks[idx];
                  return (
                    <TableCell key={idx} align="center">
                      {pick ? renderBadge(pick) : <Paper sx={{ height: 76, width: 60, borderRadius: 2 }} />}
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
