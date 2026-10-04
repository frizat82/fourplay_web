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
import { useSportContext } from '../../services/sport';

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

type FieldPair = [ToggleFieldName, ToggleFieldName];

// Each outcome switch drives both its "during game" and "at final" preference — a separate switch
// for each was confusing, and nobody wants one without the other.
const SECTIONS: {
  label: string;
  title: string;
  blurb: string;
  outcomes: { outcome: 'Covering' | 'Bloody'; fields: FieldPair; description: string }[];
}[] = [
  {
    label: 'My games',
    title: 'Notify me about my games',
    blurb: 'A push when one of your picks starts winning or losing against the spread, or once that game ends.',
    outcomes: [
      { outcome: 'Covering', fields: ['notifyMineCoveringDuringGame', 'notifyMineCoveringAtFinal'], description: 'When your pick is winning against the spread' },
      { outcome: 'Bloody', fields: ['notifyMineBloodyDuringGame', 'notifyMineBloodyAtFinal'], description: 'When your pick is losing against the spread' },
    ],
  },
  {
    label: 'League activity',
    title: 'Notify me about league activity',
    blurb:
      "A push when another league member's pick starts winning or losing, or once that game ends — this affects what everyone owes or collects once the week settles.",
    outcomes: [
      { outcome: 'Covering', fields: ['notifyOthersCoveringDuringGame', 'notifyOthersCoveringAtFinal'], description: "When someone else's pick is winning against the spread" },
      { outcome: 'Bloody', fields: ['notifyOthersBloodyDuringGame', 'notifyOthersBloodyAtFinal'], description: "When someone else's pick is losing against the spread" },
    ],
  },
];

export default function NotificationsSettingsPage() {
  const toast = useToast();
  const { sport } = useSportContext();
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
        return { ...DEFAULT_VALUES, ...(await getNotificationPreferences(sport)) };
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
          await subscribeToPush(toSubscriptionRequest(existing, sport));
        } catch {
          // Best-effort — a transient network failure here shouldn't block showing the toggle.
        }
      }
      if (!cancelled) setPushStatus(existing ? 'enabled' : 'disabled');
    })();
    return () => {
      cancelled = true;
    };
  }, [sport]);

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
      await subscribeToPush(toSubscriptionRequest(subscription, sport));
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
      await putNotificationPreferences(sport, values);
      toast.push('Notification preferences saved', 'success');
    } catch {
      toast.push('Error saving notification preferences', 'error');
    }
  };

  const setGroup = (fields: ToggleFieldName[], checked: boolean) => {
    fields.forEach((name) => setValue(name, checked, { shouldDirty: true }));
  };
  const isOn = (fields: ToggleFieldName[]) => watch(fields).some(Boolean);

  const outcomeSwitch = (sectionLabel: string, outcome: 'Covering' | 'Bloody', fields: FieldPair, description: string) => {
    const color = outcome === 'Covering' ? 'success' : 'error';
    return (
      <FormControlLabel
        key={outcome}
        sx={{ alignItems: 'flex-start', ml: 0, gap: 1 }}
        control={
          <Switch
            checked={isOn(fields)}
            onChange={(e) => setGroup(fields, e.target.checked)}
            color={color}
            // Passing slotProps.input replaces MUI's default role, so restate it.
            slotProps={{ input: { role: 'switch', 'aria-label': `${sectionLabel}: ${outcome}` } }}
          />
        }
        label={
          <span>
            <Typography variant="body2" component="span" sx={{ display: 'block', fontWeight: 600, color: `${color}.main`, pt: 1 }}>
              {outcome}
            </Typography>
            <Typography variant="caption" component="span" color="text.secondary" sx={{ display: 'block' }}>
              {description}
            </Typography>
          </span>
        }
      />
    );
  };

  return (
    <Stack spacing={3} sx={{ maxWidth: 640, margin: '0 auto', paddingTop: 6 }}>
      <div>
        <Typography variant="h5">Notifications</Typography>
        <Typography variant="body2" color="text.secondary">
          These settings only apply to the {sport === 'CFB' ? 'college football' : 'NFL'} app — each IV League app has its own.
        </Typography>
      </div>

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
              {SECTIONS.map(({ label, title, blurb, outcomes }) => {
                const allFields = outcomes.flatMap((o) => o.fields);
                return (
                  <div key={label}>
                    <FormControlLabel
                      control={<Switch checked={isOn(allFields)} onChange={(e) => setGroup(allFields, e.target.checked)} />}
                      label={title}
                    />
                    <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
                      {blurb}
                    </Typography>
                    <Accordion disableGutters elevation={0} sx={{ '&:before': { display: 'none' } }}>
                      <AccordionSummary expandIcon={<ExpandMoreIcon />} sx={{ px: 0, minHeight: 36 }}>
                        <Typography variant="body2">Advanced</Typography>
                      </AccordionSummary>
                      <AccordionDetails sx={{ px: 0 }}>
                        <Stack spacing={1}>
                          {outcomes.map((o) => outcomeSwitch(label, o.outcome, o.fields, o.description))}
                        </Stack>
                      </AccordionDetails>
                    </Accordion>
                  </div>
                );
              })}

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
                  lost it.
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
