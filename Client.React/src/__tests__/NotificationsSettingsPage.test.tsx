import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi } from 'vitest';
import NotificationsSettingsPage from '../pages/account/NotificationsSettingsPage';
import type { NotificationPreferencesDto } from '../types/notifications';

const toastState = { push: vi.fn() };
vi.mock('../services/toast', () => ({ useToast: () => toastState }));

vi.mock('../api/notifications', () => ({
  getNotificationPreferences: vi.fn(),
  putNotificationPreferences: vi.fn(),
  getVapidPublicKey: vi.fn(),
  subscribeToPush: vi.fn(),
  unsubscribeFromPush: vi.fn(),
}));

vi.mock('../services/push', () => ({
  isPushSupported: vi.fn(() => true),
  isInstalledPwa: vi.fn(() => true),
  isIos: vi.fn(() => false),
  getExistingPushSubscription: vi.fn(() => Promise.resolve(null)),
  registerServiceWorker: vi.fn(),
  requestNotificationPermission: vi.fn(),
  subscribeBrowserToPush: vi.fn(),
  toSubscriptionRequest: vi.fn(),
}));

import { getNotificationPreferences, putNotificationPreferences, subscribeToPush } from '../api/notifications';
import { getExistingPushSubscription, toSubscriptionRequest } from '../services/push';
const mockedGetPreferences = vi.mocked(getNotificationPreferences);
const mockedPutPreferences = vi.mocked(putNotificationPreferences);
const mockedSubscribeToPush = vi.mocked(subscribeToPush);
const mockedGetExistingPushSubscription = vi.mocked(getExistingPushSubscription);
const mockedToSubscriptionRequest = vi.mocked(toSubscriptionRequest);

const ALL_OFF: NotificationPreferencesDto = {
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

describe('NotificationsSettingsPage', () => {
  beforeEach(() => {
    toastState.push.mockReset();
    mockedGetPreferences.mockReset();
    mockedPutPreferences.mockReset();
  });

  it('disables Save while preferences are still loading, and enables it once loaded', async () => {
    let resolveLoad: (value: NotificationPreferencesDto) => void = () => {};
    mockedGetPreferences.mockReturnValue(new Promise((resolve) => { resolveLoad = resolve; }));

    render(<NotificationsSettingsPage />);

    expect(screen.getByRole('button', { name: /save preferences/i })).toBeDisabled();

    resolveLoad(ALL_OFF);

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /save preferences/i })).not.toBeDisabled();
    });
  });

  it('reflects a loaded "mine" preference in the simple toggle', async () => {
    mockedGetPreferences.mockResolvedValue({ ...ALL_OFF, notifyMineBloodyDuringGame: true });

    render(<NotificationsSettingsPage />);

    await waitFor(() => {
      expect(screen.getByLabelText(/notify me about my games/i)).toBeChecked();
    });
    expect(screen.getByLabelText(/notify me about league activity/i)).not.toBeChecked();
  });

  it('simple-mode write-through: turning on "my games" saves all four underlying mine fields as true', async () => {
    mockedGetPreferences.mockResolvedValue(ALL_OFF);
    mockedPutPreferences.mockResolvedValue(ALL_OFF);

    render(<NotificationsSettingsPage />);
    await waitFor(() => expect(screen.getByRole('button', { name: /save preferences/i })).not.toBeDisabled());

    await userEvent.click(screen.getByLabelText(/notify me about my games/i));
    await userEvent.click(screen.getByRole('button', { name: /save preferences/i }));

    await waitFor(() => {
      expect(mockedPutPreferences).toHaveBeenCalledWith(
        expect.objectContaining({
          notifyMineBloodyDuringGame: true,
          notifyMineBloodyAtFinal: true,
          notifyMineCoveringDuringGame: true,
          notifyMineCoveringAtFinal: true,
          notifyOthersBloodyDuringGame: false,
        })
      );
    });
    expect(toastState.push).toHaveBeenCalledWith('Notification preferences saved', 'success');
  });

  it('advanced: turning off "Bloody" for my games turns off both bloody pushes, leaves covering on', async () => {
    const allOn: NotificationPreferencesDto = {
      notifyMineBloodyDuringGame: true,
      notifyMineBloodyAtFinal: true,
      notifyMineCoveringDuringGame: true,
      notifyMineCoveringAtFinal: true,
      notifyOthersBloodyDuringGame: true,
      notifyOthersBloodyAtFinal: true,
      notifyOthersCoveringDuringGame: true,
      notifyOthersCoveringAtFinal: true,
      notifyWeekResult: true,
    };
    mockedGetPreferences.mockResolvedValue(allOn);
    mockedPutPreferences.mockResolvedValue(allOn);

    render(<NotificationsSettingsPage />);
    await waitFor(() => expect(screen.getByRole('button', { name: /save preferences/i })).not.toBeDisabled());

    const [mineAdvanced] = screen.getAllByText(/^advanced$/i);
    await userEvent.click(mineAdvanced);
    await userEvent.click(screen.getByLabelText(/^my games: bloody$/i));

    await userEvent.click(screen.getByRole('button', { name: /save preferences/i }));

    await waitFor(() => {
      expect(mockedPutPreferences).toHaveBeenCalledWith(
        expect.objectContaining({
          notifyMineBloodyDuringGame: false,
          notifyMineBloodyAtFinal: false,
          notifyMineCoveringDuringGame: true,
          notifyMineCoveringAtFinal: true,
          notifyOthersBloodyDuringGame: true,
          notifyOthersBloodyAtFinal: true,
        })
      );
    });
  });

  it('advanced shows exactly one Covering and one Bloody switch per section — no during/final split', async () => {
    mockedGetPreferences.mockResolvedValue(ALL_OFF);

    render(<NotificationsSettingsPage />);
    await waitFor(() => expect(screen.getByRole('button', { name: /save preferences/i })).not.toBeDisabled());

    for (const advanced of screen.getAllByText(/^advanced$/i)) {
      await userEvent.click(advanced);
    }

    for (const section of ['My games', 'League activity']) {
      for (const outcome of ['Covering', 'Bloody']) {
        expect(screen.getAllByLabelText(new RegExp(`^${section}: ${outcome}$`, 'i'))).toHaveLength(1);
      }
    }
    expect(screen.queryAllByLabelText(/during game|at final/i)).toHaveLength(0);
    // 3 top-level toggles + 2 per section's Advanced = 7 switches total.
    expect(screen.getAllByRole('switch', { hidden: true })).toHaveLength(7);
  });

  it('shows an error toast when saving fails', async () => {
    mockedGetPreferences.mockResolvedValue(ALL_OFF);
    mockedPutPreferences.mockRejectedValue(new Error('network error'));

    render(<NotificationsSettingsPage />);
    await waitFor(() => expect(screen.getByRole('button', { name: /save preferences/i })).not.toBeDisabled());

    await userEvent.click(screen.getByRole('button', { name: /save preferences/i }));

    await waitFor(() => {
      expect(toastState.push).toHaveBeenCalledWith('Error saving notification preferences', 'error');
    });
  });

  it('shows the Enable button when push is supported but not yet subscribed', async () => {
    mockedGetPreferences.mockResolvedValue(ALL_OFF);

    render(<NotificationsSettingsPage />);

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /enable push notifications/i })).toBeInTheDocument();
    });
  });

  it('re-homes an existing browser subscription to the current user on mount (shared-device fix)', async () => {
    // A browser-level PushSubscription existing only proves THIS DEVICE was subscribed by
    // someone, possibly a previous account on a shared/public machine — the page must re-POST it
    // so the server-side row is re-homed to whoever is logged in now, not just trust it blindly.
    mockedGetPreferences.mockResolvedValue(ALL_OFF);
    const existingSubscription = { endpoint: 'https://push.example/existing' } as PushSubscription;
    mockedGetExistingPushSubscription.mockResolvedValueOnce(existingSubscription);
    const request = { endpoint: 'https://push.example/existing', p256dh: 'p', auth: 'a' };
    mockedToSubscriptionRequest.mockReturnValueOnce(request);

    render(<NotificationsSettingsPage />);

    await waitFor(() => {
      expect(mockedSubscribeToPush).toHaveBeenCalledWith(request);
    });
    expect(screen.getByText(/push notifications are enabled on this device/i)).toBeInTheDocument();
  });
});
