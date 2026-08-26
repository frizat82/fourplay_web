import { Box, Link, Typography } from '@mui/material';

const REPO_URL = 'https://github.com/frizat82/fourplay_web';

export default function VersionFooter() {
  const sha = import.meta.env.VITE_APP_VERSION;
  if (!sha) return null;
  const shortSha = sha.slice(0, 7);

  return (
    <Box component="footer" sx={{ py: 1, textAlign: 'center' }}>
      <Typography variant="caption" color="text.secondary">
        <Link href={`${REPO_URL}/commit/${sha}`} target="_blank" rel="noopener noreferrer" color="inherit">
          {shortSha}
        </Link>
      </Typography>
    </Box>
  );
}
