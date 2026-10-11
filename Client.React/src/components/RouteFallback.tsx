import { Box, CircularProgress } from '@mui/material';

/**
 * In-layout Suspense fallback (AppLayout, under the nav) while a lazily-loaded page's code
 * downloads on a cold deep link. Launch-time states that replace the whole app use LaunchScreen
 * instead, which centres on the screen. A visible spinner, not `null`, so the user never sees an
 * empty page. Client-side navigation never shows it: React Router navigates in a transition, which
 * keeps the previous page on screen until the chunk arrives — e2e specs that click through to a
 * lazy page must wait for that page itself.
 */
export default function RouteFallback() {
  return (
    <Box sx={{ display: 'flex', justifyContent: 'center', py: 6 }}>
      <CircularProgress />
    </Box>
  );
}
