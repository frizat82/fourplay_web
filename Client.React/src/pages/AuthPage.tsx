import { Typography } from '@mui/material';
import { useAuth } from '../services/auth';

export default function AuthPage() {
  const { user } = useAuth();
  return (
    <div>
      <Typography variant="h4" gutterBottom>
        You are authenticated!
      </Typography>
      <Typography variant="body1">Hello {user?.name}</Typography>
    </div>
  );
}
