import { useEffect, useState } from 'react';
import {
  Accordion,
  AccordionDetails,
  AccordionSummary,
  Alert,
  Button,
  Card,
  CardContent,
  Divider,
  FormControlLabel,
  Grid,
  Stack,
  Switch,
  Typography,
} from '@mui/material';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import { useForm, Controller } from 'react-hook-form';
import { z } from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import { getNotificationPreferences, getVapidPublicKey, putNotificationPreferences, subscribeToPush, unsubscribeFromPush } from '../../api/notifications';
import {
  getExistingPushSubscription,
  isInstalledPwa,
  isIos,
  isPushSupported,
  registerServiceWorker,
  requestNotificationPermission,
  subscribeBrowserToPush,
  toSubscriptionRequest,
} from '../../services/push';
import { useToast } from '../../services/toast';

const schema = z.object({
  notifyMineBloodyDuringGame: z.boolean(),
  notifyMineBloodyAtFinal: z.boolean(),
  notifyMineCoveringDuringGame: z.boolean(),
  notifyMineCoveringAtFinal: z.boolean(),
  notifyOthersBloodyDuringGame: z.boolean(),
  notifyOthersBloodyAtFinal: z.boolean(),
  notifyOthersCoveringDuringGame: z.boolean(),
  notifyOthersCoveringAtFinal: z.boolean(),
  notifyWeekResult: z.boolean(),
});

type FormValues = z.infer<typeof schema>;

const DEFAULT_VALUES: FormValues = {
  notifyMineBloodyDuringGame: false,
  notifyMineBloodyAtFinal: false,
  notifyMineCoveringDuringGame: false,
  notifyMineCoveringAtFinal: false,
  notifyOthersBloodyDuringGame: false,
  notifyOthersBloodyAtFinal: false,
  notifyOthersCoveringDuringGame: false,
  notifyOthersCoveringAtFinal: false,
  notifyWeekResult: false,
};

type PushStatus = 'checking' | 'unsupported' | 'ios-not-installed' | 'disabled' | 'enabled';

type ToggleFieldName = Exclude<keyof FormValues, 'notifyWeekResult'>;

const MINE_FIELDS: ToggleFieldName[] = [
  'notifyMineBloodyDuringGame',
  'notifyMineBloodyAtFinal',
  'notifyMineCoveringDuringGame',
  'notifyMineCoveringAtFinal',
];
const OTHERS_FIELDS: ToggleFieldName[] = [
  'notifyOthersBloodyDuringGame',
  'notifyOthersBloodyAtFinal',
  'notifyOthersCoveringDuringGame',
  'notifyOthersCoveringAtFinal',
];

export default function NotificationsSettingsPage() {
  const toast = useToast();
  const [pushStatus, setPushStatus] = useState<PushStatus>('checking');
  const [pushBusy, setPushBusy] = useState(false);

  const {
    control,
    handleSubmit,
    watch,
    setValue,
    formState: { isLoading, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: async () => {
      try {
        return { ...DEFAULT_VALUES, ...(await getNotificationPreferences()) };
      } catch {
        return DEFAULT_VALUES;
      }
    },
  });

  useEffect(() => {
    let cancelled = false;
    (async () => {
      if (!isPushSupported()) {
        if (!cancelled) setPushStatus('unsupported');
        return;
      }
      if (isIos() && !isInstalledPwa()) {
        if (!cancelled) setPushStatus('ios-not-installed');
        return;
      }
      const existing = await getExistingPushSubscription();
      if (existing) {
        // A browser-level subscription object existing only proves THIS DEVICE was subscribed by
        // someone at some point — on a shared/public device, that could be a previous account.
        // Re-sending it re-homes the server-side row to whoever is logged in now (see
        // PushSubscriptionService.SubscribeAsync's re-home comment), so "enabled" here always
        // means "enabled for the current user," not just "a subscription object exists."
        try {
          await subscribeToPush(toSubscriptionRequest(existing));
        } catch {
          // Best-effort — a transient network failure here shouldn't block showing the toggle.
        }
      }
      if (!cancelled) setPushStatus(existing ? 'enabled' : 'disabled');
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const handleEnablePush = async () => {
    setPushBusy(true);
    try {
      const permission = await requestNotificationPermission();
      if (permission !== 'granted') {
        toast.push('Notification permission was not granted', 'error');
        return;
      }
      const registration = await registerServiceWorker();
      const { publicKey } = await getVapidPublicKey();
      const subscription = await subscribeBrowserToPush(registration, publicKey);
      await subscribeToPush(toSubscriptionRequest(subscription));
      setPushStatus('enabled');
      toast.push('Push notifications enabled', 'success');
    } catch {
      toast.push('Could not enable push notifications', 'error');
    } finally {
      setPushBusy(false);
    }
  };

  const handleDisablePush = async () => {
    setPushBusy(true);
    try {
      const subscription = await getExistingPushSubscription();
      if (subscription) {
        await unsubscribeFromPush({ endpoint: subscription.endpoint });
        await subscription.unsubscribe();
      }
      setPushStatus('disabled');
      toast.push('Push notifications disabled', 'success');
    } catch {
      toast.push('Could not disable push notifications', 'error');
    } finally {
      setPushBusy(false);
    }
  };

  const onSubmit = async (values: FormValues) => {
    try {
      await putNotificationPreferences(values);
      toast.push('Notification preferences saved', 'success');
    } catch {
      toast.push('Error saving notification preferences', 'error');
    }
  };

  const mineOn = watch(MINE_FIELDS).some(Boolean);
  const othersOn = watch(OTHERS_FIELDS).some(Boolean);

  const setGroup = (fields: ToggleFieldName[], checked: boolean) => {
    fields.forEach((name) => setValue(name, checked, { shouldDirty: true }));
  };

  const toggleField = (name: ToggleFieldName) => (
    <Controller
      name={name}
      control={control}
      render={({ field }) => (
        <FormControlLabel
          control={<Switch checked={field.value ?? false} onChange={(e) => field.onChange(e.target.checked)} color={name.includes('Bloody') ? 'error' : 'success'} />}
          label={name.includes('During') ? 'During game' : 'At final'}
        />
      )}
    />
  );

  return (
    <Stack spacing={3} sx={{ maxWidth: 640, margin: '0 auto', paddingTop: 6 }}>
      <Typography variant="h5">Notifications</Typography>

      <Card>
        <CardContent>
          <Typography variant="subtitle1" gutterBottom>
            Push notifications
          </Typography>
          {pushStatus === 'unsupported' && <Alert severity="warning">This browser does not support push notifications.</Alert>}
          {pushStatus === 'ios-not-installed' && (
            <Alert severity="info">
              On iPhone, add IV League to your Home Screen first (Share button &rarr; Add to Home Screen), then come back here to turn on notifications.
            </Alert>
          )}
          {pushStatus === 'disabled' && (
            <Button variant="contained" disabled={pushBusy} onClick={handleEnablePush}>
              Enable push notifications
            </Button>
          )}
          {pushStatus === 'enabled' && (
            <Stack direction="row" spacing={2} alignItems="center">
              <Alert severity="success" sx={{ flexGrow: 1 }}>
                Push notifications are enabled on this device.
              </Alert>
              <Button variant="outlined" color="error" disabled={pushBusy} onClick={handleDisablePush}>
                Disable
              </Button>
            </Stack>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardContent>
          <form onSubmit={handleSubmit(onSubmit)}>
            <Stack spacing={1}>
              <FormControlLabel
                control={<Switch checked={mineOn} onChange={(e) => setGroup(MINE_FIELDS, e.target.checked)} />}
                label="Notify me about my games"
              />
              <FormControlLabel
                control={<Switch checked={othersOn} onChange={(e) => setGroup(OTHERS_FIELDS, e.target.checked)} />}
                label="Notify me about league activity"
              />
              <Controller
                name="notifyWeekResult"
                control={control}
                render={({ field }) => (
                  <FormControlLabel
                    control={<Switch checked={field.value ?? false} onChange={(e) => field.onChange(e.target.checked)} />}
                    label="Notify me when my week is final"
                  />
                )}
              />

              <Accordion sx={{ mt: 1 }}>
                <AccordionSummary expandIcon={<ExpandMoreIcon />}>
                  <Typography>Advanced</Typography>
                </AccordionSummary>
                <AccordionDetails>
                  <Stack spacing={2}>
                    <div>
                      <Typography variant="subtitle2" gutterBottom>
                        My picks
                      </Typography>
                      <Grid container spacing={1}>
                        {MINE_FIELDS.map((name) => (
                          <Grid size={6} key={name}>
                            {toggleField(name)}
                          </Grid>
                        ))}
                      </Grid>
                    </div>
                    <Divider />
                    <div>
                      <Typography variant="subtitle2" gutterBottom>
                        Other league members
                      </Typography>
                      <Grid container spacing={1}>
                        {OTHERS_FIELDS.map((name) => (
                          <Grid size={6} key={name}>
                            {toggleField(name)}
                          </Grid>
                        ))}
                      </Grid>
                    </div>
                  </Stack>
                </AccordionDetails>
              </Accordion>

              <Button variant="contained" type="submit" disabled={isLoading || isSubmitting} sx={{ mt: 2 }}>
                Save preferences
              </Button>
            </Stack>
          </form>
        </CardContent>
      </Card>
    </Stack>
  );
}
