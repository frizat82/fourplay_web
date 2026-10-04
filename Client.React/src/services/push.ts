import type { PushSubscriptionRequestDto } from '../types/notifications';
import { isStandalonePwa } from '../utils/pwa';
import { decodeBase64Url } from '../utils/base64';
import type { SportType } from './sport';

export function isPushSupported(): boolean {
  return (
    typeof window !== 'undefined' &&
    typeof navigator !== 'undefined' &&
    'serviceWorker' in navigator &&
    'PushManager' in window &&
    'Notification' in window
  );
}

/** iOS requires the PWA to be added to the Home Screen before Web Push works at all. */
export function isInstalledPwa(): boolean {
  return typeof window !== 'undefined' && isStandalonePwa();
}

export function isIos(): boolean {
  return typeof navigator !== 'undefined' && /iphone|ipad|ipod/i.test(navigator.userAgent);
}

export async function registerServiceWorker(): Promise<ServiceWorkerRegistration> {
  return navigator.serviceWorker.register('/sw.js');
}

export async function requestNotificationPermission(): Promise<NotificationPermission> {
  return Notification.requestPermission();
}

function urlBase64ToUint8Array(base64: string): Uint8Array {
  const raw = decodeBase64Url(base64);
  const output = new Uint8Array(raw.length);
  for (let i = 0; i < raw.length; i += 1) output[i] = raw.charCodeAt(i);
  return output;
}

export async function getExistingPushSubscription(): Promise<PushSubscription | null> {
  if (!isPushSupported()) return null;
  const registration = await navigator.serviceWorker.getRegistration();
  return (await registration?.pushManager.getSubscription()) ?? null;
}

/** Named distinctly from api/notifications.ts's subscribeToPush (the "store this on our server"
 * network call) — this one talks to the browser's own PushManager. */
export async function subscribeBrowserToPush(
  registration: ServiceWorkerRegistration,
  vapidPublicKey: string
): Promise<PushSubscription> {
  const existing = await registration.pushManager.getSubscription();
  if (existing) return existing;
  return registration.pushManager.subscribe({
    userVisibleOnly: true,
    // TS's lib.dom types Uint8Array's buffer as ArrayBufferLike (which includes SharedArrayBuffer)
    // while PushSubscriptionOptionsInit wants a plain ArrayBuffer-backed BufferSource — a type-only
    // mismatch, since this Uint8Array is never actually backed by a SharedArrayBuffer.
    applicationServerKey: urlBase64ToUint8Array(vapidPublicKey) as BufferSource,
  });
}

export function toSubscriptionRequest(subscription: PushSubscription, sport: SportType): PushSubscriptionRequestDto {
  const json = subscription.toJSON();
  return {
    sport: sport === 'CFB' ? 1 : 0,
    endpoint: subscription.endpoint,
    p256dh: json.keys?.p256dh ?? '',
    auth: json.keys?.auth ?? '',
    userAgent: typeof navigator !== 'undefined' ? navigator.userAgent : undefined,
  };
}
