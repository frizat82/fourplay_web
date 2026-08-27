import { useNavigate } from 'react-router-dom';
import { Button, Stack, Typography } from '@mui/material';

export default function LockoutPage() {
  const navigate = useNavigate();

  return (
    <Stack spacing={2} sx={{ maxWidth: 520, margin: '0 auto', paddingTop: 6 }}>
      <Typography variant="h4" color="error">
        Locked out
      </Typography>
      <Typography variant="body1" color="error">
        This account has been locked out, please try again later.
      </Typography>
      <Button variant="contained" onClick={() => navigate('/account/login')}>
        Back to login
      </Button>
    </Stack>
  );
}
