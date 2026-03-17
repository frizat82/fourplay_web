import { Alert } from '@mui/material';

export default function StatusMessage({ message }: { message?: string | null }) {
  if (!message) return null;
  return <Alert variant="outlined" severity="info">{message}</Alert>;
}
