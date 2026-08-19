import { Alert, type AlertColor } from '@mui/material';

export default function StatusMessage({
  message,
  severity = 'info',
}: {
  message?: string | null;
  severity?: AlertColor;
}) {
  if (!message) return null;
  return <Alert variant="outlined" severity={severity}>{message}</Alert>;
}
