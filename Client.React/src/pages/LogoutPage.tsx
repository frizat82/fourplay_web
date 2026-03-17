import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { CircularProgress, Stack, Typography } from '@mui/material';
import { useAuth } from '../services/auth';

export default function LogoutPage() {
  const { logout } = useAuth();
  const navigate = useNavigate();

  useEffect(() => {
    let active = true;
    const fallback = window.setTimeout(() => {
      if (active) {
        navigate('/account/login', { replace: true });
      }
    }, 3500);

    const run = async () => {
      try {
        await logout();
      } finally {
        if (active) {
          window.clearTimeout(fallback);
          navigate('/account/login', { replace: true });
        }
      }
    };
    void run();

    return () => {
      active = false;
      window.clearTimeout(fallback);
    };
  }, [logout, navigate]);

  return (
    <Stack spacing={2} alignItems="center" justifyContent="center" sx={{ minHeight: 240 }}>
      <CircularProgress />
      <Typography variant="body2">Logging out...</Typography>
    </Stack>
  );
}
