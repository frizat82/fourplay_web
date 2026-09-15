import { Alert, Button, Snackbar } from '@mui/material';

interface UpdateBannerProps {
  mismatch: boolean;
}

// frizat-ndz: anchored top-center with no iOS safe-area offset, this banner (Refresh button
// included) rendered under/behind the Dynamic Island on notched/Dynamic-Island devices —
// unreachable exactly when a user needs to tap it to pick up a new deploy. Reuses this app's
// existing --safe-inset-top CSS variable (AppLayout.tsx's fixed AppBar convention) rather than a
// one-off value — env(safe-area-inset-top) is a standard CSS API, not iOS-specific: it resolves
// to a real inset on any device/browser reporting a display cutout (including some Android
// phones) and safely resolves to 0 everywhere else, so this can't regress a device without one.
const snackbarSx = { top: 'calc(var(--safe-inset-top) + 24px)' } as const;

export default function UpdateBanner({ mismatch }: UpdateBannerProps) {
  if (!mismatch) return null;

  return (
    <Snackbar open anchorOrigin={{ vertical: 'top', horizontal: 'center' }} sx={snackbarSx}>
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
