import { useState } from 'react';
import { Button, Card, CardContent, Stack, TextField, Typography } from '@mui/material';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import { requestConfirmEmail } from '../../api/auth';
import StatusMessage from '../../components/StatusMessage';

const schema = z.object({
  email: z.string().email('Invalid email'),
});

type FormValues = z.infer<typeof schema>;

export default function ResendEmailConfirmationPage() {
  const {
    register,
    handleSubmit,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { email: '' },
  });
  const [message, setMessage] = useState<string | null>(null);

  const onSubmit = async (values: FormValues) => {
    const result = await requestConfirmEmail({
      confirmationUrl: 'Account/ConfirmEmail',
      email: values.email,
    });
    setMessage(result);
    setValue('email', '');
  };

  return (
    <Stack spacing={3} sx={{ maxWidth: 520, margin: '0 auto', paddingTop: 6 }}>
      <Typography variant="h4">Resend email confirmation</Typography>
      <Typography variant="body1">Enter your email.</Typography>
      <StatusMessage message={message} />
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
