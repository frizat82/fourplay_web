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
  const fieldColor = isRedZone ? 'error.dark' : 'success.dark';

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
        <Box sx={{ flex: 1, position: 'relative', bgcolor: fieldColor }}>
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
              left: `${yardLine}%`,
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
