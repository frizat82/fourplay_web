import { useMemo, useState } from 'react';
import { useLocation } from 'react-router-dom';
import { Button, Card, CardContent, Stack, TextField, Typography } from '@mui/material';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import { requestConfirmEmail } from '../../api/auth';
import StatusMessage from '../../components/StatusMessage';
import { buildAbsoluteUrl } from '../../utils/url';
import { extractApiErrorMessage } from '../../utils/apiError';

const schema = z.object({
  email: z.string().email('Invalid email'),
});

type FormValues = z.infer<typeof schema>;

export default function ResendEmailConfirmationPage() {
  const location = useLocation();
  const prefilledEmail = useMemo(() => new URLSearchParams(location.search).get('email') ?? '', [location.search]);

  const {
    register,
    handleSubmit,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { email: prefilledEmail },
  });
  const [message, setMessage] = useState<string | null>(null);
  const [severity, setSeverity] = useState<'info' | 'error'>('info');

  const onSubmit = async (values: FormValues) => {
    try {
      const result = await requestConfirmEmail({
        confirmationUrl: buildAbsoluteUrl('/account/confirmemail'),
        email: values.email,
      });
      setMessage(result);
      setSeverity('info');
      setValue('email', '');
    } catch (error) {
      // This endpoint is rate-limited (Program.cs "forgot" policy), so a 429 with no body is a
      // real response the caller must handle, not just the always-200 "if registered" response.
      setMessage(extractApiErrorMessage(error, 'Something went wrong. Please try again.'));
      setSeverity('error');
    }
  };

  return (
    <Stack spacing={3} sx={{ maxWidth: 520, margin: '0 auto', paddingTop: 6 }}>
      <Typography variant="h4">Resend email confirmation</Typography>
      <Typography variant="body1">
        Your account isn't confirmed yet. Enter your email to get a new confirmation link.
      </Typography>
      <StatusMessage message={message} severity={severity} />
      <Card>
        <CardContent>
          <form onSubmit={handleSubmit(onSubmit)}>
            <Stack spacing={2}>
              <TextField
                label="Email"
                placeholder="name@example.com"
                {...register('email')}
                error={Boolean(errors.email)}
                helperText={errors.email?.message}
              />
              <Button variant="contained" type="submit" disabled={isSubmitting}>
                Resend
              </Button>
            </Stack>
          </form>
        </CardContent>
      </Card>
    </Stack>
  );
}
