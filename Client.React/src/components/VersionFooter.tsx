import { Box, Typography } from '@mui/material';

export default function VersionFooter() {
  const sha = import.meta.env.VITE_APP_VERSION;
  if (!sha) return null;
  const shortSha = sha.slice(0, 7);

  return (
    <Box component="footer" sx={{ py: 1, textAlign: 'center' }}>
      <Typography variant="caption" color="text.secondary">
        {shortSha}
      </Typography>
    </Box>
  );
}
