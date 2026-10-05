// Hand-rolled service worker for Web Push only — no offline/precache needs, so a workbox-based
// build plugin would add a dependency for no benefit here. Served from /sw.js (public/, not
// bundled by Vite) so its scope covers the whole origin.

self.addEventListener('install', () => {
  self.skipWaiting();
});

self.addEventListener('activate', (event) => {
  event.waitUntil(self.clients.claim());
});

// NFL (apex) and CFB (cfb. subdomain) are different origins, so each gets its own independent
// service worker registration — this one picks the right icon at runtime the same way
// index.html picks the right manifest, rather than needing two separate sw.js files. A service
// worker can't import app code, so this re-derives the same cfb-subdomain check as
// useSportContext() (src/services/sport.tsx) — keep the two in sync if that convention ever changes.
function resolveIcon() {
  return self.location.hostname.startsWith('cfb.') ? '/icon-192-cfb.png' : '/icon-192-nfl.png';
}

self.addEventListener('push', (event) => {
  let payload = { title: 'IV League', body: '' };
  if (event.data) {
    try {
      payload = { ...payload, ...event.data.json() };
    } catch {
      payload.body = event.data.text();
    }
  }

  const icon = resolveIcon();
  event.waitUntil(
    self.registration.showNotification(payload.title || 'IV League', {
      body: payload.body || '',
      icon,
      badge: icon,
      data: { url: payload.url || '/' },
    })
  );
});

self.addEventListener('notificationclick', (event) => {
  event.notification.close();
  const targetUrl = event.notification.data && event.notification.data.url ? event.notification.data.url : '/';

  event.waitUntil(
    self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then((clientList) => {
      // App already open (on any page): take that window to the game rather than just focusing
      // whatever page it was on. Falls back to opening a new window.
      for (const client of clientList) {
        if ('navigate' in client && 'focus' in client) {
          return client.navigate(targetUrl).then((c) => (c || client).focus()).catch(() => client.focus());
        }
      }
      if (self.clients.openWindow) return self.clients.openWindow(targetUrl);
      return undefined;
    })
  );
});

// Push subscriptions can silently expire or rotate — re-subscribe and tell the server about the
// new one here, rather than going silent until the user happens to revisit the settings page.
self.addEventListener('pushsubscriptionchange', (event) => {
  event.waitUntil(
    (async () => {
      const applicationServerKey = event.oldSubscription && event.oldSubscription.options
        ? event.oldSubscription.options.applicationServerKey
        : null;
      if (!applicationServerKey) return;

      const oldEndpoint = event.oldSubscription ? event.oldSubscription.endpoint : null;
      const newSubscription = await self.registration.pushManager.subscribe({
        userVisibleOnly: true,
        applicationServerKey,
      });
      const json = newSubscription.toJSON();

      // Independent server-side effects (store the new row, drop the old one) — run in parallel
      // rather than awaiting one after the other.
      const requests = [
        fetch('/api/notifications/subscribe', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          credentials: 'include',
          body: JSON.stringify({
            endpoint: newSubscription.endpoint,
            p256dh: (json.keys && json.keys.p256dh) || '',
            auth: (json.keys && json.keys.auth) || '',
            // Same host-based sport detection as services/sport.tsx — 0 = NFL, 1 = CFB.
            sport: self.location.hostname.startsWith('cfb.') ? 1 : 0,
          }),
        }),
      ];

      if (oldEndpoint && oldEndpoint !== newSubscription.endpoint) {
        requests.push(
          fetch('/api/notifications/subscribe', {
            method: 'DELETE',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'include',
            body: JSON.stringify({ endpoint: oldEndpoint }),
          })
        );
      }

      await Promise.all(requests);
    })()
  );
});
