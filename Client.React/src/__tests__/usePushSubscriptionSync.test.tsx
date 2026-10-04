import { renderHook, waitFor } from '@testing-library/react';
import { vi } from 'vitest';
import { usePushSubscriptionSync } from '../services/usePushSubscriptionSync';

vi.mock('../api/notifications', () => ({ subscribeToPush: vi.fn(() => Promise.resolve()) }));
vi.mock('../services/push', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../services/push')>()),
  getExistingPushSubscription: vi.fn(),
}));

import { subscribeToPush } from '../api/notifications';
import { getExistingPushSubscription } from '../services/push';
const mockedSubscribe = vi.mocked(subscribeToPush);
const mockedExisting = vi.mocked(getExistingPushSubscription);
const existing = { endpoint: 'https://push.example/ep', toJSON: () => ({ keys: { p256dh: 'p', auth: 'a' } }) } as unknown as PushSubscription;

// Re-registers this device's existing subscription on every app launch, tagged with this app's
// sport — so devices subscribed before sport was tracked get labeled (and a shared device moves
// to whoever is signed in now) without anyone visiting the Notifications page.
describe('usePushSubscriptionSync', () => {
  beforeEach(() => {
    mockedSubscribe.mockClear();
    mockedExisting.mockReset();
  });

  it('re-registers an existing subscription with this app\'s sport when signed in', async () => {
    mockedExisting.mockResolvedValue(existing);
    renderHook(() => usePushSubscriptionSync(true, 'CFB'));
    await waitFor(() => expect(mockedSubscribe).toHaveBeenCalledWith(expect.objectContaining({ endpoint: 'https://push.example/ep', sport: 1 })));
  });

  it('does nothing when signed out', async () => {
    mockedExisting.mockResolvedValue(existing);
    renderHook(() => usePushSubscriptionSync(false, 'NFL'));
    await new Promise((r) => setTimeout(r, 0));
    expect(mockedExisting).not.toHaveBeenCalled();
    expect(mockedSubscribe).not.toHaveBeenCalled();
  });

  it('does nothing when this device has no subscription', async () => {
    mockedExisting.mockResolvedValue(null);
    renderHook(() => usePushSubscriptionSync(true, 'NFL'));
    await waitFor(() => expect(mockedExisting).toHaveBeenCalled());
    expect(mockedSubscribe).not.toHaveBeenCalled();
  });
});
