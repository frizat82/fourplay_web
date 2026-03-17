import { Button, Card, CardContent, Stack, TextField, Typography } from '@mui/material';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import { useAuth } from '../../services/auth';
import { changePassword } from '../../api/auth';
import { useSession } from '../../services/session';
import { useNavigate } from 'react-router-dom';
import { useToast } from '../../services/toast';

const schema = z
  .object({
    oldPassword: z.string().min(1, 'Current password is required'),
    newPassword: z
      .string()
      .min(6, 'Password must be at least 6 characters')
      .regex(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).+$/, {
        message: 'Password must contain lowercase, uppercase, digit, and special character',
      }),
    confirmPassword: z.string(),
  })
  .refine((data) => data.newPassword === data.confirmPassword, {
    message: 'The new password and confirmation password do not match.',
    path: ['confirmPassword'],
  });

type FormValues = z.infer<typeof schema>;

export default function ChangePasswordPage() {
  const { user, logout } = useAuth();
  const { clearSession } = useSession();
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
    if (!user?.name) {
      toast.push('User not found', 'error');
      return;
    }
    try {
      await changePassword({
        email: user.name,
        currentPassword: values.oldPassword,
        password: values.newPassword,
      });
      toast.push('Password updated', 'success');
      await logout();
      clearSession();
      navigate('/', { replace: true });
    } catch {
      toast.push('Error updating password', 'error');
    }
  };

  return (
    <Stack spacing={3} sx={{ maxWidth: 520, margin: '0 auto', paddingTop: 6 }}>
      <Typography variant="h5">Change password</Typography>
      <Card>
        <CardContent>
          <form onSubmit={handleSubmit(onSubmit)}>
            <Stack spacing={2}>
              <TextField
                label="Old Password"
                type="password"
                {...register('oldPassword')}
                error={Boolean(errors.oldPassword)}
                helperText={errors.oldPassword?.message}
              />
              <TextField
                label="New Password"
                type="password"
                {...register('newPassword')}
                error={Boolean(errors.newPassword)}
                helperText={errors.newPassword?.message}
              />
              <TextField
                label="Confirm Password"
                type="password"
                {...register('confirmPassword')}
                error={Boolean(errors.confirmPassword)}
                helperText={errors.confirmPassword?.message}
              />
              <Button variant="contained" type="submit" disabled={isSubmitting}>
                Update password
              </Button>
            </Stack>
          </form>
        </CardContent>
      </Card>
    </Stack>
  );
}
