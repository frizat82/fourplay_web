import {
  Box,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography,
  createSvgIcon,
  useTheme,
  useMediaQuery,
} from '@mui/material';
import { alpha, type Theme } from '@mui/material/styles';
import LockOutlinedIcon from '@mui/icons-material/LockOutlined';
import { useMemo, type ReactNode } from 'react';
import type { GameView, PickView } from '../services/sportAdapter';
import { stickyColumnSx } from '../utils/tableStyles';
import { buildSurvivorRows, formatMargin, type MatrixPickState, type MatrixSlot, type SurvivorStatus } from '../utils/survivorMatrix';
import TeamLogo from './sports/TeamLogo';
import { getTeamArtMode } from '../utils/teamArt';

const SkullIcon = createSvgIcon(
  <path fillRule="evenodd" d="M12 2C7 2 4 5.6 4 10c0 2.6 1.1 4.5 3 5.6V19c0 .6.4 1 1 1h1v-2h2v2h2v-2h2v2h1c.6 0 1-.4 1-1v-3.4c1.9-1.1 3-3 3-5.6 0-4.4-3-8-8-8zM8.5 13a2 2 0 1 1 0-4 2 2 0 0 1 0 4zm7 0a2 2 0 1 1 0-4 2 2 0 0 1 0 4zM12 16.5l-1.2-2.2h2.4L12 16.5z" />,
  'Skull',
);

interface UserPicksMatrixProps {
  sport: 'nfl' | 'cfb';
  picks: PickView[];
  games: GameView[];
  requiredPicks: number;
  currentUserId?: string;
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
  mobile: { badgeHeight: 50, badgeWidth: 44, borderRadius: 1.5, logoSize: 22, teamFontLong: 12, teamFontShort: 15, pickLabelFont: 8, liveTagFont: 7, cellPadding: { px: 0.5, py: 0.5 }, usernameFont: 13 },
  desktop: { badgeHeight: 76, badgeWidth: 60, borderRadius: 2, logoSize: 32, teamFontLong: 16, teamFontShort: 22, pickLabelFont: 11, liveTagFont: 9, cellPadding: undefined, usernameFont: undefined },
};

const OUT_OPACITY = 0.45;
const LEGEND_SWATCH_SX = { width: 18, height: 14, borderRadius: 0.5, flexShrink: 0, display: 'flex', alignItems: 'center', justifyContent: 'center' };

const STATUS_LABEL: Record<SurvivorStatus, string> = {
  alive: 'alive',
  onTheRopes: 'alive, but not covering right now',
  out: 'out',
};

function describePick(state: MatrixPickState, margin: number | null): string {
  const marginText = margin == null ? '' : ` ${formatMargin(margin)}`;
  switch (state) {
    case 'won': return 'covered';
    case 'lost': return "didn't cover";
    case 'covering': return `covering${marginText} (live)`;
    case 'trailing': return `not covering${marginText} (live)`;
    default: return 'not started';
  }
}

// Final results are solid; live ones are striped and outlined so a Sunday-night pick never looks
// already decided. TeamHelmet's dark-mode logos are neon assets designed to glow against a dark
// background, so the solid tones come from the active theme rather than fixed light literals.
function stateSx(state: MatrixPickState, theme: Theme) {
  const isDark = theme.palette.mode === 'dark';
  const neutral = isDark ? 'grey.800' : 'grey.200';
  if (state === 'won') return { backgroundColor: isDark ? 'success.dark' : 'success.light' };
  if (state === 'lost') return { backgroundColor: isDark ? 'error.dark' : 'error.light' };
  if (state === 'covering' || state === 'trailing') {
    const main = (state === 'covering' ? theme.palette.success : theme.palette.error).main;
    return {
      backgroundColor: neutral,
      backgroundImage: `repeating-linear-gradient(135deg, ${alpha(main, 0.3)} 0 6px, ${alpha(main, 0.12)} 6px 12px)`,
      boxShadow: `inset 0 0 0 2px ${main}`,
    };
  }
  return { backgroundColor: neutral };
}

export default function UserPicksMatrix({ sport, picks, games, requiredPicks, currentUserId }: UserPicksMatrixProps) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'), { noSsr: true });
  const size = isMobile ? MATRIX_SIZE.mobile : MATRIX_SIZE.desktop;
  const badgeShellSx = { height: size.badgeHeight, width: size.badgeWidth, borderRadius: size.borderRadius };
  // Read once per render, not once per cell — VITE_TEAM_ART_MODE is a build-time constant that
  // can't change mid-render, and this table renders one cell per (user × requiredPicks).
  const isLogosMode = getTeamArtMode() === 'logos';
  const rows = useMemo(() => buildSurvivorRows(picks, games, requiredPicks, currentUserId), [picks, games, requiredPicks, currentUserId]);
  const neutralSx = stateSx('pending', theme);

  const renderSlot = (slot: MatrixSlot) => {
    if (slot.kind === 'empty') return <Paper sx={{ ...badgeShellSx, ...neutralSx }} />;
    if (slot.kind === 'hidden') {
      return (
        <Paper aria-label="Hidden until kickoff" sx={{ ...badgeShellSx, ...neutralSx, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
          <LockOutlinedIcon sx={{ fontSize: size.logoSize * 0.7, color: 'text.secondary' }} />
        </Paper>
      );
    }

    const isLive = slot.state === 'covering' || slot.state === 'trailing';
    const typeLabel = slot.pickType === 'Spread' ? '' : slot.pickType.toUpperCase();
    const subLabel = [typeLabel, slot.margin == null ? '' : formatMargin(slot.margin)].filter(Boolean).join(' ');
    const ariaType = slot.pickType === 'Spread' ? '' : ` ${slot.pickType.toLowerCase()}`;

    return (
      <Paper
        aria-label={`${slot.team}${ariaType}: ${describePick(slot.state, slot.margin)}`}
        sx={{
          ...badgeShellSx,
          ...stateSx(slot.state, theme),
          position: 'relative',
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          justifyContent: 'center',
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
          <TeamLogo abbr={slot.team} sport={sport} size={size.logoSize} showLabel />
        ) : (
          <Typography sx={{ fontSize: slot.team.length > 3 ? size.teamFontLong : size.teamFontShort, fontWeight: 800, letterSpacing: '0.02em' }}>
            {slot.team}
          </Typography>
        )}
        {/* frizat: the badge used to signal win/loss three ways at once — the tinted background,
            a colored OVER/UNDER label, and a corner check/cancel icon — reported as cluttered
            and hard to read. The background alone carries that signal; this label stays the
            same neutral ink as the team name above it, in every case. */}
        {subLabel && (
          <Typography sx={{ fontSize: size.pickLabelFont, fontWeight: 700, letterSpacing: '0.03em' }}>
            {subLabel}
          </Typography>
        )}
        {/* Straddles the top edge. aria-hidden: the badge's own label already says "(live)". */}
        {isLive && (
          <Box
            aria-hidden
            sx={{
              position: 'absolute', top: 0, left: '50%', transform: 'translate(-50%, -50%)',
              bgcolor: 'error.main', color: 'common.white', borderRadius: '4px', px: 0.5,
              fontSize: size.liveTagFont, fontWeight: 700, letterSpacing: '0.08em', lineHeight: 1.3,
            }}
          >
            LIVE
          </Box>
        )}
      </Paper>
    );
  };

  const swatch = (state: MatrixPickState, icon?: ReactNode) => (
    <Box sx={{ ...LEGEND_SWATCH_SX, ...stateSx(state, theme) }}>{icon}</Box>
  );
  const legend: Array<{ label: string; swatch: ReactNode }> = [
    { label: 'Covered', swatch: swatch('won') },
    { label: "Didn't cover", swatch: swatch('lost') },
    { label: 'Covering now (live)', swatch: swatch('covering') },
    { label: 'Not covering now (live)', swatch: swatch('trailing') },
    { label: 'Hidden until kickoff', swatch: swatch('pending', <LockOutlinedIcon sx={{ fontSize: 11, color: 'text.secondary' }} />) },
    { label: 'Out (faded, listed last)', swatch: <SkullIcon sx={{ fontSize: 14, color: 'text.secondary' }} /> },
  ];

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
          {rows.map(({ userId, userName, status, slots }) => {
            const isOut = status === 'out';
            return (
              <TableRow key={userId}>
                <TableCell sx={{ ...stickyColumnSx, ...size.cellPadding }}>
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75 }}>
                    <Box role="img" aria-label={`${userName}: ${STATUS_LABEL[status]}`} sx={{ display: 'inline-flex', width: 14, justifyContent: 'center', flexShrink: 0 }}>
                      {isOut ? (
                        <SkullIcon sx={{ fontSize: 14, color: 'text.secondary' }} />
                      ) : (
                        <Box sx={{ width: 8, height: 8, borderRadius: '50%', bgcolor: status === 'alive' ? 'success.main' : 'error.main' }} />
                      )}
                    </Box>
                    <Typography fontWeight={600} fontSize={size.usernameFont} color={isOut ? 'text.secondary' : undefined}>
                      {userName}
                    </Typography>
                  </Box>
                </TableCell>
                {/* frizat: Over/Under is an alternate pick TYPE for one game, not an additional
                    pick — AddPicks' server-side validation caps total picks (any type) at
                    requiredPicks, so one slot per required-pick column. */}
                {slots.map((slot, idx) => (
                  <TableCell key={idx} align="center" sx={{ ...size.cellPadding, opacity: isOut ? OUT_OPACITY : undefined }}>
                    {renderSlot(slot)}
                  </TableCell>
                ))}
              </TableRow>
            );
          })}
        </TableBody>
      </Table>
      <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1.5, px: 2, py: 1.5, borderTop: 1, borderColor: 'divider' }}>
        {legend.map(({ label, swatch: icon }) => (
          <Box key={label} sx={{ display: 'flex', alignItems: 'center', gap: 0.75 }}>
            {icon}
            <Typography variant="caption" color="text.secondary">{label}</Typography>
          </Box>
        ))}
      </Box>
    </Paper>
  );
}
