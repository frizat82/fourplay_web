import type { ReactNode } from 'react';
import { Box, CircularProgress } from '@mui/material';

/**
 * Whole-screen, dead-centre container for every state that stands in for the entire app at launch
 * — the session check's spinner, its "Can't reach" prompt, the first route chunk. The static splash
 * in index.html centres its spinner the same way, so the hand-off from splash to app doesn't jump.
 * Defaults to a spinner.
 */
export default function LaunchScreen({ children = <CircularProgress /> }: { children?: ReactNode }) {
  return (
    <Box
      data-testid="launch-screen"
      sx={{ display: 'grid', placeItems: 'center', minHeight: '100vh', '@supports (min-height: 100dvh)': { minHeight: '100dvh' } }}
    >
      {children}
    </Box>
  );
}
