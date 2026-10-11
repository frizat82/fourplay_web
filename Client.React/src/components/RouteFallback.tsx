import { Box, CircularProgress } from '@mui/material';

interface Props {
  /**
   * Centre the spinner in the whole screen. For loading states that replace the entire app at
   * launch (the session check, the first route chunk): the static splash in index.html centres
   * its spinner the same way, so the hand-off from splash to app doesn't make it jump.
   */
  fullScreen?: boolean;
}

/**
 * Suspense fallback while a lazily-loaded route's code downloads on a cold load (App.tsx). A
 * visible spinner, not `null`, so the user never sees an empty page. Client-side navigation never
 * shows it: React Router navigates in a transition, which keeps the previous page on screen until
 * the chunk arrives — e2e specs that click through to a lazy page must wait for that page itself.
 */
export default function RouteFallback({ fullScreen = false }: Props) {
  return (
    <Box
      sx={
        fullScreen
          ? { display: 'grid', placeItems: 'center', minHeight: '100vh', '@supports (min-height: 100dvh)': { minHeight: '100dvh' } }
          : { display: 'flex', justifyContent: 'center', py: 6 }
      }
    >
      <CircularProgress />
    </Box>
  );
}
