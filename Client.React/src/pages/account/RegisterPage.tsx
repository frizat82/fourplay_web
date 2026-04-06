import { useEffect, useMemo, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { Alert, Button, Card, CardActions, CardContent, Stack, TextField, Typography } from '@mui/material';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import { createUser } from '../../api/auth';
import { validateInvitation } from '../../api/invitations';
import { useToast } from '../../services/toast';

const schema = z
  .object({
    invitationCode: z.string().min(1, 'Invitation code is required'),
    userName: z.string().min(1, 'User name is required'),
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
    message: 'Passwords do not match',
    path: ['confirmPassword'],
  });

type FormValues = z.infer<typeof schema>;

export default function RegisterPage() {
  const toast = useToast();
  const location = useLocation();
  const navigate = useNavigate();
  const params = useMemo(() => new URLSearchParams(location.search), [location.search]);
  const inviteCode = params.get('inviteCode') ?? '';
  const returnUrl = params.get('returnUrl') ?? '/';
  const [leagueName, setLeagueName] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      invitationCode: inviteCode,
      userName: '',
      email: '',
      password: '',
      confirmPassword: '',
    },
  });

  useEffect(() => {
    document.title = 'Register';
    if (inviteCode) {
      void validateInvitation(inviteCode).then((inv) => {
        if (inv?.leagueName) setLeagueName(inv.leagueName);
      });
    }
  }, [inviteCode]);

  const onSubmit = async (values: FormValues) => {
    const result = await createUser({
      email: values.email,
      code: values.invitationCode,
      password: values.password,
      username: values.userName,
    });

    if (!result.isSuccess) {
      toast.push(result.errors.join('\n') || 'Registration failed', 'error');
      return;
    }

    toast.push('User created successfully, check email for confirmation', 'success');
    navigate(`/account/registerconfirmation?email=${encodeURIComponent(values.email)}&returnUrl=${encodeURIComponent(returnUrl)}`);
  };

  return (
    <Stack spacing={3} sx={{ maxWidth: 640, margin: '0 auto', paddingTop: 6 }}>
      <Typography variant="h4">Register</Typography>
      {leagueName && (
        <Alert severity="info">
          You've been invited to join <strong>{leagueName}</strong>
        </Alert>
      )}
      <Card>
        <CardContent>
          <Stack spacing={2} component="form" onSubmit={handleSubmit(onSubmit)}>
            <TextField
              label="Invitation Code"
              helperText={errors.invitationCode?.message ?? 'Enter your invitation code'}
              {...register('invitationCode')}
              error={Boolean(errors.invitationCode)}
            />
            <TextField
              label="Username"
              {...register('userName')}
              error={Boolean(errors.userName)}
              helperText={errors.userName?.message}
            />
            <TextField
              label="Email"
              {...register('email')}
              error={Boolean(errors.email)}
              helperText={errors.email?.message}
            />
            <TextField
              label="Password"
              type="password"
              helperText={errors.password?.message ?? 'Must contain letters, numbers, and special characters'}
              {...register('password')}
              error={Boolean(errors.password)}
            />
            <TextField
              label="Confirm Password"
              type="password"
              helperText={errors.confirmPassword?.message}
              {...register('confirmPassword')}
              error={Boolean(errors.confirmPassword)}
            />
            <CardActions sx={{ justifyContent: 'flex-end', p: 0 }}>
              <Button type="submit" variant="contained" disabled={isSubmitting}>
                {isSubmitting ? 'Registering…' : 'Register'}
              </Button>
            </CardActions>
          </Stack>
        </CardContent>
      </Card>
      <Typography variant="body2" color="text.secondary">
        Need an invitation? This site is invite-only. Please contact an administrator to request an invitation.
      </Typography>
    </Stack>
  );
}
