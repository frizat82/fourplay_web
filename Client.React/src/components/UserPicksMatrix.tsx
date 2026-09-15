import {
  Paper,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography,
  useTheme,
  useMediaQuery,
} from '@mui/material';
import type { NflPickDto, SpreadCalculationResponse } from '../types/picks';
import { stickyColumnSx } from '../utils/tableStyles';
import TeamLogo from './sports/TeamLogo';
import { getTeamArtMode } from '../utils/teamArt';

interface UserPicksMatrixProps {
  sport: 'nfl' | 'cfb';
  users: string[];
  picks: NflPickDto[];
  spreads: Record<string, SpreadCalculationResponse>;
  requiredPicks: number;
}

// frizat-2aa: badges fixed at 76x60 + a 32px logo + default MUI table padding worked fine on a
// laptop but ate the entire 390px iOS viewport per column — with the app's 4-pick regular-season
// weeks, only 2-3 of 4 "Pick" columns fit before the required horizontal scroll started, and only
// ~4 rows of a 5+ member league fit vertically. Roughly half-size on mobile, unchanged on
// desktop — noSsr per this repo's useMediaQuery convention (CLAUDE.md), though this is a pure
// client SPA so SSR doesn't actually apply; kept for consistency with the rest of the codebase.
//
// One config object (not per-property ternaries scattered through the JSX) so every mobile-
// varying value lives in one place. Kept as a JS isMobile lookup, not MUI sx breakpoint objects
// (WeekYearSelector.tsx's usual pure-CSS pattern) — this component's tests assert exact pixel
// dimensions via a mocked window.matchMedia, which doesn't reliably drive real CSS @media rules
// in jsdom, so a pure-CSS version would lose the ability to verify the fix.
const MATRIX_SIZE = {
  mobile: { badgeHeight: 50, badgeWidth: 44, borderRadius: 1.5, logoSize: 22, teamFontLong: 12, teamFontShort: 15, pickLabelFont: 8, cellPadding: { px: 0.5, py: 0.5 }, usernameFont: 13 },
  desktop: { badgeHeight: 76, badgeWidth: 60, borderRadius: 2, logoSize: 32, teamFontLong: 16, teamFontShort: 22, pickLabelFont: 11, cellPadding: undefined, usernameFont: undefined },
};

export default function UserPicksMatrix({ sport, users, picks, spreads, requiredPicks }: UserPicksMatrixProps) {
  const theme = useTheme();
  const isDark = theme.palette.mode === 'dark';
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'), { noSsr: true });
  const size = isMobile ? MATRIX_SIZE.mobile : MATRIX_SIZE.desktop;
  const badgeShellSx = { height: size.badgeHeight, width: size.badgeWidth, borderRadius: size.borderRadius };
  // Read once per render, not once per cell — VITE_TEAM_ART_MODE is a build-time constant that
  // can't change mid-render, and this table renders one cell per (user × requiredPicks).
  const isLogosMode = getTeamArtMode() === 'logos';

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
          ...badgeShellSx,
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
            the fixed 60px badge width rather than overflowing it. frizat-cwj: that lesson only
            applies to the icon-sized synthetic badge — a real logo at 32px plus its own label
            stays readable, so 'logos' mode gets both instead of text-only. */}
        {isLogosMode ? (
          <TeamLogo abbr={pick.team} sport={sport} size={size.logoSize} showLabel />
        ) : (
          <Typography sx={{ fontSize: pick.team.length > 3 ? size.teamFontLong : size.teamFontShort, fontWeight: 800, letterSpacing: '0.02em' }}>
            {pick.team}
          </Typography>
        )}
        {/* frizat: the badge used to signal win/loss three ways at once — the tinted background,
            a colored OVER/UNDER label, and a corner check/cancel icon — reported as cluttered
            and hard to read. The background alone now carries that signal; this label stays the
            same neutral ink as the team name above it, in every case, with only weight/size
            marking it as secondary information (which team > which bet type). */}
        {pick.pick !== 'Spread' && (
          <Typography sx={{ fontSize: size.pickLabelFont, fontWeight: 700, letterSpacing: '0.03em' }}>
            {pick.pick.toUpperCase()}
          </Typography>
        )}
      </Paper>
    );
  };

  return (
    <Paper sx={{ overflowX: 'auto' }}>
      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell sx={{ ...stickyColumnSx, ...size.cellPadding }}>User</TableCell>
            {Array.from({ length: requiredPicks }).map((_, idx) => (
              <TableCell key={idx} sx={size.cellPadding}>Pick {idx + 1}</TableCell>
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
                <TableCell sx={{ ...stickyColumnSx, ...size.cellPadding }}>
                  <Typography fontWeight={600} fontSize={size.usernameFont}>{user}</Typography>
                </TableCell>
                {Array.from({ length: requiredPicks }).map((_, idx) => {
                  const pick = userPicks[idx];
                  return (
                    <TableCell key={idx} align="center" sx={size.cellPadding}>
                      {pick ? renderBadge(pick) : <Paper sx={badgeShellSx} />}
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
