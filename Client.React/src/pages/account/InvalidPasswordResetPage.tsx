import { Stack, Typography } from '@mui/material';

export default function InvalidPasswordResetPage() {
  return (
    <Stack spacing={2} sx={{ maxWidth: 520, margin: '0 auto', paddingTop: 6 }}>
      <Typography variant="h4">Invalid password reset</Typography>
      <Typography variant="body1">The password reset link is invalid.</Typography>
    </Stack>
  );
}
