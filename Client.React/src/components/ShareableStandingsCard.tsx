import { forwardRef } from 'react';
import { Box, Typography } from '@mui/material';

interface ShareableStandingsCardProps {
  leagueName: string;
  userName: string;
  rank: string;
  total: number;
}

// Fixed brand look (not theme-reactive) — this gets exported as a static PNG shared outside
// the app, so it should look the same regardless of the viewer's own light/dark preference,
// the same way an Instagram story template doesn't follow the poster's OS theme.
const ShareableStandingsCard = forwardRef<HTMLDivElement, ShareableStandingsCardProps>(
  ({ leagueName, userName, rank, total }, ref) => {
    const sign = Math.sign(total);
    const totalColor = sign > 0 ? '#10b981' : sign < 0 ? '#ef4444' : '#ffffff';
    const totalLabel = sign > 0 ? `+${total}` : `${total}`;

    return (
      <Box
        ref={ref}
        sx={{
          width: 600,
          height: 600,
          background: 'linear-gradient(135deg, #1a2847 0%, #0f1729 100%)',
          color: '#ffffff',
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          justifyContent: 'center',
          gap: 2,
          textAlign: 'center',
          p: 4,
        }}
      >
        <Typography sx={{ color: '#ff6b35', fontWeight: 700, letterSpacing: 3, fontSize: 20 }}>
          IV LEAGUE
        </Typography>
        <Typography sx={{ fontSize: 26, opacity: 0.8 }}>{leagueName}</Typography>
        <Typography sx={{ fontSize: 44, fontWeight: 800, mt: 2 }}>{userName}</Typography>
        <Typography sx={{ fontSize: 22, opacity: 0.7 }}>{`#${rank}`}</Typography>
        <Typography sx={{ fontSize: 72, fontWeight: 900, color: totalColor, mt: 2 }}>
          {totalLabel}
        </Typography>
      </Box>
    );
  }
);
ShareableStandingsCard.displayName = 'ShareableStandingsCard';

export default ShareableStandingsCard;
