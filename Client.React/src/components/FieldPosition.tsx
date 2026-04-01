import { Box, Typography } from '@mui/material';
import SportsFootballIcon from '@mui/icons-material/SportsFootball';
import type { GameSituation } from '../types/liveGame';

interface FieldPositionProps {
  situation: GameSituation | null | undefined;
}

export default function FieldPosition({ situation }: FieldPositionProps) {
  if (!situation) return null;

  const { yardLine, isHomePossession, isRedZone, downDistanceText } = situation;

  return (
    <Box sx={{ mt: 1 }}>
      <Box
        data-testid="field-position-bar"
        data-redzone={String(isRedZone)}
        sx={{
          position: 'relative',
          height: 20,
          borderRadius: 1,
          bgcolor: isRedZone ? 'error.dark' : 'success.dark',
          opacity: 0.85,
          mx: 1,
        }}
      >
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
      <Typography variant="caption" color="text.secondary" align="center" display="block" sx={{ mt: 0.5 }}>
        {downDistanceText}
      </Typography>
    </Box>
  );
}
