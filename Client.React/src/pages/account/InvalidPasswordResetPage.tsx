import { useNavigate } from 'react-router-dom';
import { Button, Stack, Typography } from '@mui/material';

export default function InvalidPasswordResetPage() {
  const navigate = useNavigate();

  return (
    <Stack spacing={2} sx={{ maxWidth: 520, margin: '0 auto', paddingTop: 6 }}>
      <Typography variant="h4">Invalid password reset</Typography>
      <Typography variant="body1">The password reset link is invalid or has expired.</Typography>
      <Button variant="contained" onClick={() => navigate('/account/forgotpassword')}>
        Request a new reset link
      </Button>
      <Button variant="text" onClick={() => navigate('/account/login')}>
        Back to login
      </Button>
    </Stack>
  );
}
