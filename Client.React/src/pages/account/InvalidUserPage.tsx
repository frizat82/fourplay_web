import { useNavigate } from 'react-router-dom';
import { Button, Stack, Typography } from '@mui/material';

export default function InvalidUserPage() {
  const navigate = useNavigate();

  return (
    <Stack spacing={2} sx={{ maxWidth: 520, margin: '0 auto', paddingTop: 6 }}>
      <Typography variant="h4">Invalid user</Typography>
      <Typography variant="body1">Invalid user.</Typography>
      <Button variant="contained" onClick={() => navigate('/account/login')}>
        Go to login
      </Button>
    </Stack>
  );
}
