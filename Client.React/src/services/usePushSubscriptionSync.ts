import { useEffect } from 'react';
import { subscribeToPush } from '../api/notifications';
import { getExistingPushSubscription, toSubscriptionRequest } from './push';
import type { SportType } from './sport';

// On every launch of a signed-in app, re-register this device's existing push subscription with
// the server, tagged with this app's sport. Labels devices subscribed before sport was tracked
// (so NFL alerts stop reaching the CFB app and vice versa) and moves a shared device to whoever is
// signed in now. Best-effort and silent — never prompts, never blocks the app.
export function usePushSubscriptionSync(isSignedIn: boolean, sport: SportType) {
  useEffect(() => {
    if (!isSignedIn) return;
    (async () => {
      try {
        const existing = await getExistingPushSubscription();
        if (existing) await subscribeToPush(toSubscriptionRequest(existing, sport));
      } catch {
        // Next launch retries.
      }
    })();
  }, [isSignedIn, sport]);
}
