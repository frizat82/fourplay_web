import { Stack, Typography } from '@mui/material';

export default function LockoutPage() {
  return (
    <Stack spacing={2} sx={{ maxWidth: 520, margin: '0 auto', paddingTop: 6 }}>
      <Typography variant="h4" color="error">
        Locked out
      </Typography>
      <Typography variant="body1" color="error">
        This account has been locked out, please try again later.
      </Typography>
    </Stack>
  );
}
