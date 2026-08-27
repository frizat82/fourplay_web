import { useEffect, useMemo, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { Alert, Button, Card, CardActions, CardContent, Stack, TextField, Typography } from '@mui/material';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import { createUser } from '../../api/auth';
import { validateInvitation } from '../../api/invitations';
import { useToast } from '../../services/toast';
import { buildAbsoluteUrl } from '../../utils/url';
import { extractApiErrorMessage } from '../../utils/apiError';

const baseSchema = z.object({
  invitationCode: z.string(),
  userName: z.string().min(1, 'User name is required'),
  email: z.string().email('Invalid email'),
  password: z
    .string()
    .min(6, 'Password must be at least 6 characters')
    .regex(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).+$/, {
      message: 'Password must contain lowercase, uppercase, digit, and special character',
    }),
  confirmPassword: z.string(),
});

type FormValues = z.infer<typeof baseSchema>;

export default function RegisterPage() {
  const toast = useToast();
  const location = useLocation();
  const navigate = useNavigate();
  const params = useMemo(() => new URLSearchParams(location.search), [location.search]);
  const inviteCode = params.get('inviteCode') ?? '';
  const inviteLinkToken = params.get('inviteLinkToken') ?? '';
  const isLinkFlow = Boolean(inviteLinkToken);
  const returnUrl = params.get('returnUrl') ?? '/';
  const [leagueName, setLeagueName] = useState<string | null>(null);

  const schema = useMemo(
    () =>
      baseSchema
        .superRefine((data, ctx) => {
          if (!isLinkFlow && !data.invitationCode) {
            ctx.addIssue({ code: z.ZodIssueCode.custom, message: 'Invitation code is required', path: ['invitationCode'] });
          }
        })
        .refine((data) => data.password === data.confirmPassword, {
          message: 'Passwords do not match',
          path: ['confirmPassword'],
        }),
    [isLinkFlow],
  );

  const {
    register,
    handleSubmit,
    setValue,
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
      void validateInvitation(inviteCode)
        .then((inv) => {
          if (inv?.leagueName) setLeagueName(inv.leagueName);
          if (inv?.email) setValue('email', inv.email);
        })
        .catch(() => {
          // Preview lookup failure (e.g. a stale/expired invite code) is non-fatal — the
          // real validation happens server-side on submit; just skip the league-name preview.
        });
    }
  }, [inviteCode, setValue]);

  const onSubmit = async (values: FormValues) => {
    try {
      const result = await createUser({
        email: values.email,
        code: values.invitationCode,
        password: values.password,
        username: values.userName,
        confirmationUrl: buildAbsoluteUrl('/account/confirmemail'),
        ...(inviteLinkToken ? { inviteLinkToken } : {}),
      });

      if (!result.isSuccess) {
        toast.push(result.errors.join('\n') || 'Registration failed', 'error');
        return;
      }

      toast.push('User created successfully, check email for confirmation', 'success');
      navigate(`/account/registerconfirmation?email=${encodeURIComponent(values.email)}&returnUrl=${encodeURIComponent(returnUrl)}`);
    } catch (error) {
      // The real backend returns a non-2xx status (400 for a bad invite code, 429 when
      // rate-limited) rather than 200 with isSuccess:false, so createUser() rejects here.
      toast.push(extractApiErrorMessage(error, 'Registration failed'), 'error');
    }
  };

  return (
    <Stack spacing={3} sx={{ maxWidth: 640, margin: '0 auto', paddingTop: 6 }}>
      {/* frizat: standalone PWA mode has no browser chrome to fall back on — this route renders
          outside AppLayout (no header/nav), so without an explicit link back, a visitor who
          opened this from a stale/reused invite and changes their mind has no way out. */}
      <Button
        variant="text"
        onClick={() => navigate('/')}
        sx={{ alignSelf: 'flex-start', opacity: 0.75 }}
      >
        ← Back to Home
      </Button>
      <Typography variant="h4">Register</Typography>
      {leagueName && (
        <Alert severity="info">
          You're registering for IV League and joining <strong>{leagueName}</strong>.
        </Alert>
      )}
      <Card>
        <CardContent>
          <Stack spacing={2} component="form" onSubmit={handleSubmit(onSubmit)}>
            {!isLinkFlow && (
              <TextField
                label="Invitation Code"
                helperText={errors.invitationCode?.message ?? 'Enter your invitation code'}
                {...register('invitationCode')}
                error={Boolean(errors.invitationCode)}
              />
            )}
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
      <Button variant="text" onClick={() => navigate('/account/login')} sx={{ alignSelf: 'center' }}>
        Already have an account? Log in
      </Button>
    </Stack>
  );
}
