import { useNavigate } from 'react-router-dom';
import { Button, Card, CardContent, Stack, TextField, Typography } from '@mui/material';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import { forgotPassword } from '../../api/auth';
import { useToast } from '../../services/toast';

const schema = z.object({
  email: z.string().email('Invalid email'),
});

type FormValues = z.infer<typeof schema>;

export default function ForgotPasswordPage() {
  const navigate = useNavigate();
  const toast = useToast();
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
  });

  const onSubmit = async (values: FormValues) => {
    try {
      await forgotPassword({
        email: values.email,
        resetUrl: new URL('/account/resetpassword', window.location.origin).toString(),
      });
      navigate('/account/forgotpasswordconfirmation', { replace: true });
    } catch {
      toast.push('Error requesting password reset', 'error');
    }
  };

  return (
    <Stack spacing={3} sx={{ maxWidth: 520, margin: '0 auto', paddingTop: 6 }}>
      <Typography variant="h4">Forgot your password?</Typography>
      <Typography variant="body1">Enter your email.</Typography>
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
                {isSubmitting ? 'Submitting…' : 'Reset password'}
              </Button>
            </Stack>
          </form>
        </CardContent>
      </Card>
    </Stack>
  );
}
