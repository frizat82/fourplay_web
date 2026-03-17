import { Stack, Typography } from '@mui/material';

export default function ForgotPasswordConfirmationPage() {
  return (
    <Stack spacing={2} sx={{ maxWidth: 520, margin: '0 auto', paddingTop: 6 }}>
      <Typography variant="h4">Forgot password confirmation</Typography>
      <Typography variant="body1">Please check your email to reset your password.</Typography>
    </Stack>
  );
}
