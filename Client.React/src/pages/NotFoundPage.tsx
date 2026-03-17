import { Button, Stack, Typography } from '@mui/material';
import { Link as RouterLink } from 'react-router-dom';

export default function NotFoundPage() {
  return (
    <Stack spacing={2} alignItems="flex-start">
      <Typography variant="h4">Page not found</Typography>
      <Typography variant="body1" color="text.secondary">
        The page you’re looking for doesn’t exist.
      </Typography>
      <Button variant="contained" component={RouterLink} to="/">
        Back to dashboard
      </Button>
    </Stack>
  );
}
