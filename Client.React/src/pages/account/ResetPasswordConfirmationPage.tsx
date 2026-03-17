import { Button, Stack, Typography } from '@mui/material';
import { useNavigate } from 'react-router-dom';

export default function ResetPasswordConfirmationPage() {
  const navigate = useNavigate();
  return (
    <Stack spacing={2} sx={{ maxWidth: 520, margin: '0 auto', paddingTop: 6 }}>
      <Typography variant="h4">Reset password confirmation</Typography>
      <Typography variant="body1">
        Your password has been reset. Please click below to log in.
      </Typography>
      <Button variant="contained" onClick={() => navigate('/account/login')}>
        Go to login
      </Button>
    </Stack>
  );
}
