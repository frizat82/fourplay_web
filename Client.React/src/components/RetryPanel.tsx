import { useState } from 'react';
import { Box, Button, Typography } from '@mui/material';

interface Props {
  title: string;
  message: string;
  actionLabel: string;
  onAction: () => void | Promise<void>;
}

/**
 * The app's one "something went wrong, try again" screen — used when a page fails to render
 * (RouteErrorBoundary) and when the server can't be reached at all (auth). The button stays
 * disabled while an async action is in flight so repeated taps on a weak signal don't stack up.
 */
export default function RetryPanel({ title, message, actionLabel, onAction }: Props) {
  const [busy, setBusy] = useState(false);
  const act = async () => {
    setBusy(true);
    try {
      await onAction();
    } finally {
      setBusy(false);
    }
  };
  return (
    <Box sx={{ textAlign: 'center', py: 6, px: 2 }}>
      <Typography variant="h6" gutterBottom>{title}</Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>{message}</Typography>
      <Button variant="contained" onClick={() => void act()} disabled={busy}>{actionLabel}</Button>
    </Box>
  );
}
