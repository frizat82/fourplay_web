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
import { useForm, Controller, type Control } from 'react-hook-form';
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

// A small labeled table instead of 4 identically-captioned "During game"/"At final" switches
// distinguished only by color — the row label ("Covering"/"Bloody") makes the color a
// reinforcement, never the only signal.
function OutcomeToggleGrid({ control, sectionLabel, duringField, duringLabel, finalField, finalLabel, color }: {
  control: Control<FormValues>;
  sectionLabel: string;
  duringField: ToggleFieldName;
  finalField: ToggleFieldName;
  duringLabel: string;
  finalLabel: string;
  color: 'success' | 'error';
}) {
  const rowLabel = color === 'success' ? 'Covering' : 'Bloody';
  return (
    <Grid container spacing={1} alignItems="center">
      <Grid size={4}>
        <Typography variant="body2" sx={{ color: `${color}.main`, fontWeight: 600 }}>
          {rowLabel}
        </Typography>
      </Grid>
      <Grid size={4}>
        <Controller
          name={duringField}
          control={control}
          render={({ field }) => (
            <FormControlLabel
              control={
                <Switch
                  checked={field.value ?? false}
                  onChange={(e) => field.onChange(e.target.checked)}
                  color={color}
                  size="small"
                  slotProps={{ input: { 'aria-label': `${sectionLabel}: ${rowLabel} ${duringLabel}` } }}
                />
              }
              label={duringLabel}
              slotProps={{ typography: { variant: 'caption', sx: { display: { xs: 'none', sm: 'inline' } } } }}
            />
          )}
        />
      </Grid>
      <Grid size={4}>
        <Controller
          name={finalField}
          control={control}
          render={({ field }) => (
            <FormControlLabel
              control={
                <Switch
                  checked={field.value ?? false}
                  onChange={(e) => field.onChange(e.target.checked)}
                  color={color}
                  size="small"
                  slotProps={{ input: { 'aria-label': `${sectionLabel}: ${rowLabel} ${finalLabel}` } }}
                />
              }
              label={finalLabel}
              slotProps={{ typography: { variant: 'caption', sx: { display: { xs: 'none', sm: 'inline' } } } }}
            />
          )}
        />
      </Grid>
    </Grid>
  );
}

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
            <Stack spacing={1} divider={<Divider />}>
              <div>
                <FormControlLabel
                  control={<Switch checked={mineOn} onChange={(e) => setGroup(MINE_FIELDS, e.target.checked)} />}
                  label="Notify me about my games"
                />
                <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
                  A push when one of your picks starts winning or losing against the spread, or once that game ends.
                </Typography>
                <Accordion disableGutters elevation={0} sx={{ '&:before': { display: 'none' } }}>
                  <AccordionSummary expandIcon={<ExpandMoreIcon />} sx={{ px: 0, minHeight: 36 }}>
                    <Typography variant="body2">Advanced: when exactly</Typography>
                  </AccordionSummary>
                  <AccordionDetails sx={{ px: 0 }}>
                    <Stack spacing={1}>
                      <OutcomeToggleGrid control={control} sectionLabel="My games" color="success" duringField="notifyMineCoveringDuringGame" duringLabel="During game" finalField="notifyMineCoveringAtFinal" finalLabel="At final" />
                      <OutcomeToggleGrid control={control} sectionLabel="My games" color="error" duringField="notifyMineBloodyDuringGame" duringLabel="During game" finalField="notifyMineBloodyAtFinal" finalLabel="At final" />
                    </Stack>
                  </AccordionDetails>
                </Accordion>
              </div>

              <div>
                <FormControlLabel
                  control={<Switch checked={othersOn} onChange={(e) => setGroup(OTHERS_FIELDS, e.target.checked)} />}
                  label="Notify me about league activity"
                />
                <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
                  A push when another league member's pick starts winning or losing, or once that game ends — this
                  affects what everyone owes or collects once the week settles.
                </Typography>
                <Accordion disableGutters elevation={0} sx={{ '&:before': { display: 'none' } }}>
                  <AccordionSummary expandIcon={<ExpandMoreIcon />} sx={{ px: 0, minHeight: 36 }}>
                    <Typography variant="body2">Advanced: when exactly</Typography>
                  </AccordionSummary>
                  <AccordionDetails sx={{ px: 0 }}>
                    <Stack spacing={1}>
                      <OutcomeToggleGrid control={control} sectionLabel="League activity" color="success" duringField="notifyOthersCoveringDuringGame" duringLabel="During game" finalField="notifyOthersCoveringAtFinal" finalLabel="At final" />
                      <OutcomeToggleGrid control={control} sectionLabel="League activity" color="error" duringField="notifyOthersBloodyDuringGame" duringLabel="During game" finalField="notifyOthersBloodyAtFinal" finalLabel="At final" />
                    </Stack>
                  </AccordionDetails>
                </Accordion>
              </div>

              <div>
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
                <Typography variant="body2" color="text.secondary">
                  One push once every game in your week is decided — "You Won the Week!" or naming the pick that
                  lost it. No Advanced setting for this one; it only ever fires once.
                </Typography>
              </div>
            </Stack>

            <Button variant="contained" type="submit" disabled={isLoading || isSubmitting} sx={{ mt: 3 }}>
              Save preferences
            </Button>
          </form>
        </CardContent>
      </Card>
    </Stack>
  );
}
