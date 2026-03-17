import { useEffect, useMemo, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { Alert, Card, CardContent, Stack, Typography } from '@mui/material';
import { confirmEmail } from '../../api/auth';
import { decodeBase64Url, isValidBase64Url } from '../../utils/base64';

export default function ConfirmEmailPage() {
  const location = useLocation();
  const navigate = useNavigate();
  const params = useMemo(() => new URLSearchParams(location.search), [location.search]);
  const userId = params.get('userId');
  const code = params.get('code');

  const [statusMessage, setStatusMessage] = useState<string | null>(null);
  const [isError, setIsError] = useState(false);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const run = async () => {
      if (!userId || !code) {
        setIsError(true);
        setStatusMessage('Invalid confirmation link.');
        setLoading(false);
        setTimeout(() => navigate('/', { replace: true }), 3000);
        return;
      }

      if (!isValidBase64Url(code)) {
        setIsError(true);
        setStatusMessage('Error Invalid confirmation code format.');
        setLoading(false);
        return;
      }

      try {
        const decodedToken = decodeBase64Url(code);
        await confirmEmail({ userId, token: decodedToken });
        setIsError(false);
        setStatusMessage('Thank you for confirming your email. You can now log in.');
        setLoading(false);
        setTimeout(() => navigate('/account/login', { replace: true }), 3000);
      } catch {
        setIsError(true);
        setStatusMessage('Error confirming your email.');
        setLoading(false);
      }
    };

    void run();
  }, [code, navigate, userId]);

  return (
    <Stack spacing={2} sx={{ maxWidth: 520, margin: '0 auto', paddingTop: 6 }}>
      <Card>
        <CardContent>
          <Typography variant="h5" sx={{ mb: 2 }}>
            Email Confirmation
          </Typography>
          {statusMessage && (
            <Alert severity={isError ? 'error' : 'success'}>{statusMessage}</Alert>
          )}
          {loading && (
            <Typography variant="body2" sx={{ mt: 2 }}>
              Confirming your email...
            </Typography>
          )}
        </CardContent>
      </Card>
    </Stack>
  );
}
