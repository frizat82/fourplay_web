// Pure request-routing rules for the app-shell cache in sw.js, kept separate so they can be unit
// tested (src/__tests__/swRouting.test.ts evaluates this file against a fake `self`). A classic
// script pulled in with importScripts(), not an ES module — module service workers are unreliable
// on older iOS Safari.
(function (self) {
  // KILL SWITCH: set to false and deploy to turn the app-shell cache off — every request goes
  // straight to the network and the next activate deletes all of our caches. Push is unaffected.
  var CACHE_ENABLED = true;
  // Bump a version to drop that cache wholesale on the next activate.
  var SHELL_CACHE = 'ivl-shell-v1';
  var ASSET_CACHE = 'ivl-assets-v1';

  self.IVLSwRouting = {
    SHELL_CACHE: SHELL_CACHE,
    ASSET_CACHE: ASSET_CACHE,
    // Vercel serves index.html for every app route, so one cached copy covers them all.
    SHELL_URL: '/index.html',
    // How long a launch waits on the network before opening the saved copy instead.
    NAVIGATION_TIMEOUT_MS: 3500,
    // Hashed build files accumulate across deploys; keep the newest this many.
    MAX_ASSET_ENTRIES: 150,

    /**
     * 'shell' — a page load (served network-first, saved copy on a stall);
     * 'asset' — a hashed, immutable build file (served from cache first);
     * 'passthrough' — everything else, including every /api call, which must always be live.
     */
    classifyRequest: function (request, origin) {
      if (!CACHE_ENABLED || request.method !== 'GET') return 'passthrough';
      var url = new URL(request.url);
      if (url.origin !== origin) return 'passthrough';
      if (url.pathname.indexOf('/api/') === 0) return 'passthrough';
      if (request.mode === 'navigate') return 'shell';
      if (url.pathname.indexOf('/assets/') === 0) return 'asset';
      return 'passthrough';
    },

    isCacheableResponse: function (response, kind) {
      // Safari refuses a redirected response for a navigation, so never save one.
      if (response.status !== 200 || response.type !== 'basic' || response.redirected) return false;
      var isHtml = (response.headers.get('content-type') || '').indexOf('text/html') !== -1;
      // A shell must be HTML. An "asset" that comes back as HTML is the SPA rewrite answering
      // for a file this deploy no longer has — caching that would break the page for good.
      return kind === 'shell' ? isHtml : !isHtml;
    },

    cachesToDelete: function (keys) {
      return keys.filter(function (key) {
        return key.indexOf('ivl-') === 0 && (!CACHE_ENABLED || (key !== SHELL_CACHE && key !== ASSET_CACHE));
      });
    },

    // The page's own build files, so a freshly installed worker can save what this launch already
    // downloaded before it was in control.
    assetUrlsInShell: function (html) {
      return Array.from(new Set(html.match(/\/assets\/[\w.-]+/g) || []));
    },

    // Cache keys come back in insertion order, so the oldest are first.
    entriesToTrim: function (entries, max) {
      return entries.length > max ? entries.slice(0, entries.length - max) : [];
    },
  };
})(self);
