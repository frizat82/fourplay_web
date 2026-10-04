import { describe, it, expect, vi, afterEach } from 'vitest';
import {
  isPushSupported,
  isInstalledPwa,
  isIos,
  subscribeBrowserToPush,
  toSubscriptionRequest,
} from '../services/push';

describe('push.ts', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    delete (navigator as Navigator & { standalone?: boolean }).standalone;
  });

  describe('isPushSupported', () => {
    it('returns false when serviceWorker is missing from navigator', () => {
      // isPushSupported only checks property presence via `in`, so the stand-in navigator must
      // genuinely lack the 'serviceWorker' key (not just hold it as undefined).
      vi.stubGlobal('navigator', {});
      vi.stubGlobal('PushManager', {});
      vi.stubGlobal('Notification', {});

      expect(isPushSupported()).toBe(false);
    });

    it('returns true when serviceWorker, PushManager, and Notification are all present', () => {
      vi.stubGlobal('navigator', { serviceWorker: {} });
      vi.stubGlobal('PushManager', {});
      vi.stubGlobal('Notification', {});

      expect(isPushSupported()).toBe(true);
    });
  });

  describe('isInstalledPwa', () => {
    it('returns true when display-mode: standalone matches', () => {
      vi.stubGlobal('matchMedia', (query: string) => ({ matches: query === '(display-mode: standalone)' }));

      expect(isInstalledPwa()).toBe(true);
    });

    it('returns true when navigator.standalone is set (iOS Home Screen install)', () => {
      vi.stubGlobal('matchMedia', () => ({ matches: false }));
      Object.defineProperty(navigator, 'standalone', { value: true, configurable: true });

      expect(isInstalledPwa()).toBe(true);
    });

    it('returns false when neither signal is present', () => {
      vi.stubGlobal('matchMedia', () => ({ matches: false }));

      expect(isInstalledPwa()).toBe(false);
    });
  });

  describe('isIos', () => {
    it('returns true for an iPhone user agent', () => {
      vi.stubGlobal('navigator', { userAgent: 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X)' });
      expect(isIos()).toBe(true);
    });

    it('returns false for a non-iOS user agent', () => {
      vi.stubGlobal('navigator', { userAgent: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64)' });
      expect(isIos()).toBe(false);
    });
  });

  describe('subscribeBrowserToPush', () => {
    it('returns the existing subscription without calling subscribe() again', async () => {
      const existing = { endpoint: 'https://push.example/existing' } as PushSubscription;
      const registration = {
        pushManager: {
          getSubscription: vi.fn().mockResolvedValue(existing),
          subscribe: vi.fn(),
        },
      } as unknown as ServiceWorkerRegistration;

      const result = await subscribeBrowserToPush(registration, 'test-public-key');

      expect(result).toBe(existing);
      expect(registration.pushManager.subscribe).not.toHaveBeenCalled();
    });

    it('subscribes when no existing subscription is present', async () => {
      const created = { endpoint: 'https://push.example/new' } as PushSubscription;
      const registration = {
        pushManager: {
          getSubscription: vi.fn().mockResolvedValue(null),
          subscribe: vi.fn().mockResolvedValue(created),
        },
      } as unknown as ServiceWorkerRegistration;

      const result = await subscribeBrowserToPush(registration, 'test-public-key');

      expect(result).toBe(created);
      expect(registration.pushManager.subscribe).toHaveBeenCalledWith(
        expect.objectContaining({ userVisibleOnly: true })
      );
    });
  });

  describe('toSubscriptionRequest', () => {
    // The server only sends an NFL alert to the NFL app's subscription and a CFB alert to the CFB
    // app's — so each app must say which sport it is (LeagueType: 0 = NFL, 1 = CFB).
    it.each([['NFL', 0], ['CFB', 1]] as const)('tags the subscription with the %s app\'s sport', (sport, expected) => {
      const subscription = { endpoint: 'https://push.example/ep', toJSON: () => ({}) } as unknown as PushSubscription;
      expect(toSubscriptionRequest(subscription, sport).sport).toBe(expected);
    });

    it('maps endpoint and keys from the browser subscription', () => {
      const subscription = {
        endpoint: 'https://push.example/ep1',
        toJSON: () => ({ keys: { p256dh: 'p-key', auth: 'a-key' } }),
      } as unknown as PushSubscription;

      const result = toSubscriptionRequest(subscription, 'NFL');

      expect(result.endpoint).toBe('https://push.example/ep1');
      expect(result.p256dh).toBe('p-key');
      expect(result.auth).toBe('a-key');
    });

    it('falls back to empty strings when keys are missing', () => {
      const subscription = {
        endpoint: 'https://push.example/ep2',
        toJSON: () => ({}),
      } as unknown as PushSubscription;

      const result = toSubscriptionRequest(subscription, 'NFL');

      expect(result.p256dh).toBe('');
      expect(result.auth).toBe('');
    });
  });
});
