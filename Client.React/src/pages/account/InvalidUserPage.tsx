import { Stack, Typography } from '@mui/material';

export default function InvalidUserPage() {
  return (
    <Stack spacing={2} sx={{ maxWidth: 520, margin: '0 auto', paddingTop: 6 }}>
      <Typography variant="h4">Invalid user</Typography>
      <Typography variant="body1">Invalid user.</Typography>
    </Stack>
  );
}
