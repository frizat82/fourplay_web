import { useEffect, useMemo } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { Button, Card, CardContent, Stack, TextField, Typography } from '@mui/material';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import { resetPassword } from '../../api/auth';
import { decodeBase64Url, isValidBase64Url } from '../../utils/base64';
import { useToast } from '../../services/toast';

const schema = z
  .object({
    email: z.string().email('Invalid email'),
    password: z
      .string()
      .min(6, 'Password must be at least 6 characters')
      .regex(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).+$/, {
        message: 'Password must contain lowercase, uppercase, digit, and special character',
      }),
    confirmPassword: z.string(),
  })
  .refine((data) => data.password === data.confirmPassword, {
    message: 'The password and confirmation password do not match.',
    path: ['confirmPassword'],
  });

type FormValues = z.infer<typeof schema>;

export default function ResetPasswordPage() {
  const location = useLocation();
  const navigate = useNavigate();
  const toast = useToast();
  const params = useMemo(() => new URLSearchParams(location.search), [location.search]);
  const code = params.get('code');

  useEffect(() => {
    if (!code || !isValidBase64Url(code)) {
      navigate('/account/invalidpasswordreset', { replace: true });
    }
  }, [code, navigate]);

  const token = code && isValidBase64Url(code) ? decodeBase64Url(code) : '';

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
  });

  const onSubmit = async (values: FormValues) => {
    try {
      await resetPassword({
        email: values.email,
        password: values.password,
        token,
      });
      navigate('/account/resetpasswordconfirmation', { replace: true });
    } catch {
      toast.push('Error resetting password', 'error');
    }
  };

  return (
    <Stack spacing={3} sx={{ maxWidth: 640, margin: '0 auto', paddingTop: 6 }}>
      <Typography variant="h4" align="center">
        Reset Your Password
      </Typography>
      <Typography variant="subtitle1" align="center" color="text.secondary">
        Enter your email and choose a new password below.
      </Typography>
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
              <TextField
                label="New Password"
                type="password"
                {...register('password')}
                error={Boolean(errors.password)}
                helperText={errors.password?.message}
              />
              <TextField
                label="Confirm Password"
                type="password"
                {...register('confirmPassword')}
                error={Boolean(errors.confirmPassword)}
                helperText={errors.confirmPassword?.message}
              />
              <Button type="submit" variant="contained" disabled={isSubmitting}>
                {isSubmitting ? 'Resetting…' : 'Reset Password'}
              </Button>
            </Stack>
          </form>
        </CardContent>
      </Card>
    </Stack>
  );
}
