import { Box, CircularProgress } from '@mui/material';

/**
 * Suspense fallback while a lazily-loaded route's code downloads (App.tsx). A visible spinner, not
 * `null`: a blank fallback shows the user an empty page and, because it contains no progressbar,
 * lets e2e helpers' "wait for the spinner to clear" pass before the page has rendered at all.
 */
export default function RouteFallback() {
  return (
    <Box sx={{ display: 'flex', justifyContent: 'center', py: 6 }}>
      <CircularProgress />
    </Box>
  );
}
