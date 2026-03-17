import { useMemo } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { Button, Stack, Typography } from '@mui/material';

export default function RegisterConfirmationPage() {
  const location = useLocation();
  const navigate = useNavigate();
  const params = useMemo(() => new URLSearchParams(location.search), [location.search]);
  const email = params.get('email');

  return (
    <Stack spacing={2} sx={{ maxWidth: 520, margin: '0 auto', paddingTop: 6 }}>
      <Typography variant="h4">Register confirmation</Typography>
      <Typography variant="body1">
        {email ? `Check ${email} to confirm your account.` : 'Please check your email to confirm your account.'}
      </Typography>
      <Button variant="contained" onClick={() => navigate('/account/login')}>
        Go to login
      </Button>
    </Stack>
  );
}
