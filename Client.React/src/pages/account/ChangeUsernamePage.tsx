import { Button, Card, CardContent, Stack, TextField, Typography } from '@mui/material';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import { useAuth } from '../../services/auth';
import { changeUsername } from '../../api/auth';
import { useToast } from '../../services/toast';
import { extractApiErrorMessage } from '../../utils/apiError';

const schema = z.object({
  currentPassword: z.string().min(1, 'Current password is required'),
  newUsername: z.string().min(1, 'Username is required'),
});

type FormValues = z.infer<typeof schema>;

export default function ChangeUsernamePage() {
  const { refresh } = useAuth();
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
      await changeUsername(values);
      toast.push('Username updated', 'success');
      // Unlike ChangePasswordPage, this does NOT force a full logout — the backend already
      // reissues valid cookies with the new username baked into the JWT, so a session refresh
      // is enough to reflect the change immediately.
      await refresh();
    } catch (error) {
      toast.push(extractApiErrorMessage(error, 'Error updating username'), 'error');
    }
  };

  return (
    <Stack spacing={3} sx={{ maxWidth: 520, margin: '0 auto', paddingTop: 6 }}>
      <Typography variant="h5">Change username</Typography>
      <Card>
        <CardContent>
          <form onSubmit={handleSubmit(onSubmit)}>
            <Stack spacing={2}>
              <TextField
                label="Current Password"
                type="password"
                {...register('currentPassword')}
                error={Boolean(errors.currentPassword)}
                helperText={errors.currentPassword?.message}
              />
              <TextField
                label="New Username"
                {...register('newUsername')}
                error={Boolean(errors.newUsername)}
                helperText={errors.newUsername?.message}
              />
              <Typography variant="caption" color="text.secondary">
                This is also what you use to log in.
              </Typography>
              <Button variant="contained" type="submit" disabled={isSubmitting}>
                Update username
              </Button>
            </Stack>
          </form>
        </CardContent>
      </Card>
    </Stack>
  );
}
