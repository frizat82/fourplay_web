import { useEffect, useMemo } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { Button, Card, CardContent, Checkbox, FormControlLabel, Stack, TextField, Typography } from '@mui/material';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import { useAuth } from '../../services/auth';
import { useToast } from '../../services/toast';

const schema = z.object({
  email: z.string().min(1, 'Email is required'),
  password: z.string().min(1, 'Password is required'),
  rememberMe: z.boolean().default(false),
});

type FormValues = z.input<typeof schema>;

export default function LoginPage() {
  const { login } = useAuth();
  const toast = useToast();
  const navigate = useNavigate();
  const location = useLocation();

  const params = useMemo(() => new URLSearchParams(location.search), [location.search]);
  const returnUrl = useMemo(() => {
    const raw = params.get('returnUrl') ?? '/dashboard';
    if (!raw.startsWith('/')) return '/dashboard';
    if (raw.startsWith('//')) return '/dashboard';
    if (raw === '/logout') return '/dashboard';
    return raw;
  }, [params]);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      email: '',
      password: '',
      rememberMe: false,
    },
  });

  useEffect(() => {
    document.title = 'Log in';
  }, []);

  const onSubmit = async (values: FormValues) => {
    const result = await login({
      username: values.email,
      password: values.password,
      rememberMe: values.rememberMe ?? false,
    });
    if (!result.succeeded) {
      toast.push(result.message ?? 'Login failed', 'error');
      return;
    }
    navigate(returnUrl, { replace: true });
  };

  return (
    <Stack
      spacing={3}
      sx={{
        maxWidth: 420,
        width: '100%',
        margin: '0 auto',
        paddingTop: { xs: 4, sm: 8 },
        paddingX: { xs: 0, sm: 2 },
      }}
    >
      <Stack spacing={0.5} alignItems={{ xs: 'flex-start', sm: 'center' }}>
        <Typography
          variant="h3"
          sx={{
            fontFamily: '"Rajdhani", sans-serif',
            fontWeight: 700,
            color: '#ff6b35',
            letterSpacing: 2,
            lineHeight: 1,
          }}
        >
          FOURPLAY
        </Typography>
        <Typography variant="h5" sx={{ fontWeight: 600 }}>
          Log in
        </Typography>
      </Stack>
      <Card>
        <CardContent sx={{ p: 3 }}>
          <form onSubmit={handleSubmit(onSubmit)}>
            <Stack spacing={2}>
              <TextField
                label="Email"
                placeholder="name@example.com"
                {...register('email')}
                error={Boolean(errors.email)}
                helperText={errors.email?.message}
                autoComplete="email"
                inputMode="email"
              />
              <TextField
                label="Password"
                type="password"
                placeholder="password"
                {...register('password')}
                error={Boolean(errors.password)}
                helperText={errors.password?.message}
                autoComplete="current-password"
              />
              <FormControlLabel
                control={<Checkbox {...register('rememberMe')} sx={{ color: 'text.secondary', '&.Mui-checked': { color: '#ff6b35' } }} />}
                label="Remember me"
              />
              <Button type="submit" variant="contained" color="secondary" disabled={isSubmitting} fullWidth>
                {isSubmitting ? 'Logging in…' : 'Login'}
              </Button>
            </Stack>
          </form>
        </CardContent>
      </Card>
      <Stack direction="row" justifyContent="space-between" sx={{ px: 0.5 }}>
        <Button variant="text" onClick={() => navigate('/account/forgotpassword')} sx={{ opacity: 0.75, fontSize: '0.85rem' }}>
          Forgot your password?
        </Button>
        <Button variant="text" onClick={() => navigate('/account/register')} sx={{ opacity: 0.75, fontSize: '0.85rem' }}>
          Register
        </Button>
      </Stack>
    </Stack>
  );
}
