import { Box, Typography } from '@mui/material';
import SportsFootballIcon from '@mui/icons-material/SportsFootball';
import type { GameSituation } from '../types/liveGame';

interface FieldPositionProps {
  situation: GameSituation | null | undefined;
}

const YARD_MARKERS = [10, 20, 30, 40, 50, 60, 70, 80, 90];

export default function FieldPosition({ situation }: FieldPositionProps) {
  if (!situation) return <Box sx={{ mt: 1, height: 24 + 4 + 20, mx: 1 }} />;

  const { yardLine, isHomePossession, isRedZone, downDistanceText } = situation;
  // yardLine is a fixed coordinate measured from the HOME team's own goal, regardless of which
  // team currently has the ball (confirmed against a live game: away team with the ball at their
  // own 33 rendered as yardLine=67, i.e. 100 minus their distance from the home team's goal). The
  // home team is always drawn on the right in this layout, so this conversion is unconditional —
  // isHomePossession only decides the arrow's direction, never the position.
  const ballPositionPercent = 100 - yardLine;
  // The actual red zone is the 20 yards nearest whichever goal the ball is closest to — not the
  // whole field. yardLine<=20 is near the home goal (right side, local% 80-100); yardLine>=80 is
  // near the away goal (left side, local% 0-20). These are mutually exclusive by construction. If
  // isRedZone is true but yardLine falls in neither range (inconsistent upstream data), show no
  // stripe rather than guessing a side.
  const redZoneStripeLeft = yardLine <= 20 ? 80 : yardLine >= 80 ? 0 : null;

  return (
    <Box sx={{ mt: 1 }}>
      <Box
        data-testid="field-position-bar"
        data-redzone={String(isRedZone)}
        sx={{ display: 'flex', height: 24, mx: 1, borderRadius: 1, overflow: 'hidden', opacity: 0.9 }}
      >
        {/* Left end zone */}
        <Box sx={{ width: '8%', bgcolor: 'success.main', flexShrink: 0 }} />

        {/* Playing field */}
        <Box sx={{ flex: 1, position: 'relative', bgcolor: 'success.dark' }}>
          {/* Red zone stripe — behind the yard markers/ball marker so it doesn't obscure them */}
          {isRedZone && redZoneStripeLeft !== null && (
            <Box
              data-testid="red-zone-stripe"
              sx={{
                position: 'absolute',
                top: 0,
                bottom: 0,
                left: `${redZoneStripeLeft}%`,
                width: '20%',
                bgcolor: 'error.dark',
              }}
            />
          )}
          {/* Yard markers */}
          {YARD_MARKERS.map(yard => (
            <Box
              key={yard}
              sx={{
                position: 'absolute',
                left: `${yard}%`,
                top: 0,
                bottom: 0,
                width: '1px',
                bgcolor: 'rgba(255,255,255,0.35)',
              }}
            />
          ))}

          {/* Ball marker */}
          <Box
            data-testid="ball-marker"
            sx={{
              position: 'absolute',
              top: '50%',
              left: `${ballPositionPercent}%`,
              transform: 'translate(-50%, -50%)',
              display: 'flex',
              alignItems: 'center',
              gap: 0.25,
              color: 'white',
            }}
          >
            <Typography
              data-testid="possession-arrow"
              variant="caption"
              sx={{ lineHeight: 1, fontWeight: 700 }}
            >
              {isHomePossession ? '◀' : '▶'}
            </Typography>
            <SportsFootballIcon sx={{ fontSize: 14 }} />
          </Box>
        </Box>

        {/* Right end zone */}
        <Box sx={{ width: '8%', bgcolor: 'success.main', flexShrink: 0 }} />
      </Box>

      <Typography variant="caption" color="text.secondary" align="center" display="block" sx={{ mt: 0.5 }}>
        {downDistanceText}
      </Typography>
    </Box>
  );
}
