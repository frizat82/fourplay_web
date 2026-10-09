import { useState } from 'react';
import { Box, Button, Typography } from '@mui/material';

interface Props {
  onRetry: () => Promise<void>;
}

/** Shown when the app couldn't reach the server at all — usually a weak phone signal. */
export default function ConnectionRetry({ onRetry }: Props) {
  const [retrying, setRetrying] = useState(false);
  const retry = async () => {
    setRetrying(true);
    try {
      await onRetry();
    } finally {
      setRetrying(false);
    }
  };
  return (
    <Box sx={{ textAlign: 'center', py: 6, px: 2 }}>
      <Typography variant="h6" gutterBottom>Can't reach IV League</Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        Check your signal and try again.
      </Typography>
      <Button variant="contained" onClick={() => void retry()} disabled={retrying}>
        {retrying ? 'Retrying…' : 'Retry'}
      </Button>
    </Box>
  );
}
