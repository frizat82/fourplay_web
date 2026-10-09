// Hand-rolled service worker: Web Push, plus an app-shell cache so the installed app still opens
// on a weak signal. Without the cache, a launch whose page request stalled left iOS's home-screen
// app on a plain white screen until force-quit. Served from /sw.js (public/, not bundled by Vite)
// so its scope covers the whole origin; Vercel serves it no-cache (vercel.json) so a fix here
// reaches phones on their next launch.
//
// Caching rules (which requests, what's safe to save) live in sw-routing.js and are unit tested.
// Every /api call bypasses the worker entirely — scores, picks and auth are always live.
//
// KILL SWITCH — if the cache ever misbehaves in production: delete the 'fetch' listener below,
// and in 'activate' delete every cache whose name starts with 'ivl-' (keep the push handlers).
// Deploy; phones pick it up on their next launch and go back to plain network loading.

importScripts('/sw-routing.js');
var routing = self.IVLSwRouting;

self.addEventListener('install', (event) => {
  self.skipWaiting();
  event.waitUntil(saveShell().catch(() => {}));
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    (async () => {
      const keys = await caches.keys();
      await Promise.all(routing.cachesToDelete(keys).map((key) => caches.delete(key)));
      // Lets a launch start its page request while the worker is still waking up (iOS 15.4+).
      if (self.registration.navigationPreload) await self.registration.navigationPreload.enable();
      await self.clients.claim();
    })()
  );
});

self.addEventListener('fetch', (event) => {
  const kind = routing.classifyRequest(event.request, self.location.origin);
  if (kind === 'shell') event.respondWith(guarded(event.request, () => serveShell(event)));
  else if (kind === 'asset') event.respondWith(guarded(event.request, () => serveAsset(event.request)));
  // Anything else: no respondWith — the browser handles it exactly as if there were no worker.
});

// Any bug in the cache path must degrade to a plain network request, never a broken launch.
async function guarded(request, handler) {
  try {
    return await handler();
  } catch {
    return fetch(request);
  }
}

// Network first, but only for NAVIGATION_TIMEOUT_MS: after that, open the saved copy and let the
// network response refresh the cache in the background for next time.
function serveShell(event) {
  const network = (async () => {
    const preloaded = event.preloadResponse ? await event.preloadResponse : undefined;
    const response = preloaded || (await fetch(event.request));
    if (routing.isCacheableResponse(response, 'shell')) {
      const copy = response.clone();
      event.waitUntil(caches.open(routing.SHELL_CACHE).then((cache) => cache.put(routing.SHELL_URL, copy)).catch(() => {}));
    }
    return response;
  })();
  event.waitUntil(network.catch(() => {}));
  const saved = caches.match(routing.SHELL_URL, { cacheName: routing.SHELL_CACHE });

  return new Promise((resolve, reject) => {
    let settled = false;
    const finish = (response) => {
      if (!settled) {
        settled = true;
        resolve(response);
      }
    };
    network.then(finish, async (error) => {
      const copy = await saved;
      if (copy) finish(copy);
      else if (!settled) reject(error);
    });
    setTimeout(async () => {
      const copy = await saved;
      if (copy) finish(copy); // no saved copy yet: keep waiting on the network
    }, routing.NAVIGATION_TIMEOUT_MS);
  });
}

// Hashed build files never change, so a saved copy is always correct.
async function serveAsset(request) {
  const cache = await caches.open(routing.ASSET_CACHE);
  const saved = await cache.match(request);
  if (saved) return saved;
  const response = await fetch(request);
  if (routing.isCacheableResponse(response, 'asset')) {
    await cache.put(request, response.clone());
    const keys = await cache.keys();
    await Promise.all(routing.entriesToTrim(keys, routing.MAX_ASSET_ENTRIES).map((key) => cache.delete(key)));
  }
  return response;
}

// Saves the current page and the build files it references. Runs at install, because the launch
// that installs the worker downloaded those before the worker was in control.
async function saveShell() {
  const response = await fetch(routing.SHELL_URL, { cache: 'reload' });
  if (!routing.isCacheableResponse(response, 'shell')) return;
  const html = await response.clone().text();
  const shellCache = await caches.open(routing.SHELL_CACHE);
  await shellCache.put(routing.SHELL_URL, response);
  const assetCache = await caches.open(routing.ASSET_CACHE);
  await Promise.all(
    routing.assetUrlsInShell(html).map(async (url) => {
      if (await assetCache.match(url)) return;
      const asset = await fetch(url);
      if (routing.isCacheableResponse(asset, 'asset')) await assetCache.put(url, asset);
    })
  );
}

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
