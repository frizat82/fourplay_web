import { Alert, Button, Snackbar } from '@mui/material';

interface UpdateBannerProps {
  mismatch: boolean;
}

export default function UpdateBanner({ mismatch }: UpdateBannerProps) {
  if (!mismatch) return null;

  return (
    <Snackbar open anchorOrigin={{ vertical: 'top', horizontal: 'center' }}>
      <Alert
        severity="info"
        action={
          <Button color="inherit" size="small" onClick={() => window.location.reload()}>
            Refresh
          </Button>
        }
      >
        A new version is available
      </Alert>
    </Snackbar>
  );
}
