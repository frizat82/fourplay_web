import { Alert, Box, Button } from '@mui/material';
import PageHeader from './PageHeader';

interface QueryErrorAlertProps {
  title: string;
  entityName?: string;
  onRetry: () => void;
}

export default function QueryErrorAlert({ title, entityName = title.toLowerCase(), onRetry }: QueryErrorAlertProps) {
  return (
    <Box><PageHeader title={title} />
      <Alert
        severity="error"
        sx={{ mt: 4 }}
        action={<Button color="inherit" size="small" onClick={onRetry}>Retry</Button>}
      >
        Couldn&apos;t load {entityName}. Check your connection and try again.
      </Alert>
    </Box>
  );
}
