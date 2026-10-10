import swRoutingSrc from '../../public/sw-routing.js?raw';

// public/sw-routing.js is a classic script the service worker pulls in with importScripts(), so
// it isn't an ES module Vitest can import — evaluate it against a fake `self` instead, the same
// way the worker sees it.
interface SwRouting {
  SHELL_CACHE: string;
  ASSET_CACHE: string;
  SHELL_URL: string;
  NAVIGATION_TIMEOUT_MS: number;
  MAX_ASSET_ENTRIES: number;
  classifyRequest(request: { url: string; method: string; mode: string }, origin: string): 'shell' | 'asset' | 'passthrough';
  isCacheableResponse(
    response: { status: number; type: string; redirected: boolean; headers: { get(name: string): string | null } },
    kind: 'shell' | 'asset',
  ): boolean;
  cachesToDelete(keys: string[]): string[];
  CACHE_ENABLED: boolean;
  assetsToEvict(cachedPaths: string[], max: number, keep: string[]): string[];
  assetUrlsInShell(html: string): string[];
}

function loadRouting(src: string = swRoutingSrc): SwRouting {
  const fakeSelf: { IVLSwRouting?: SwRouting } = {};
  new Function('self', src)(fakeSelf);
  return fakeSelf.IVLSwRouting!;
}

const ORIGIN = 'https://ivleague.xyz';
const r = loadRouting();
const req = (path: string, opts: Partial<{ method: string; mode: string; origin: string }> = {}) => ({
  url: `${opts.origin ?? ORIGIN}${path}`,
  method: opts.method ?? 'GET',
  mode: opts.mode ?? 'cors',
});
const res = (opts: Partial<{ status: number; type: string; redirected: boolean; contentType: string }> = {}) => ({
  status: opts.status ?? 200,
  type: opts.type ?? 'basic',
  redirected: opts.redirected ?? false,
  headers: { get: (name: string) => (name.toLowerCase() === 'content-type' ? (opts.contentType ?? 'text/html; charset=utf-8') : null) },
});

describe('classifyRequest', () => {
  it('treats same-origin page navigations as the app shell, on any route', () => {
    expect(r.classifyRequest(req('/', { mode: 'navigate' }), ORIGIN)).toBe('shell');
    expect(r.classifyRequest(req('/picks?week=3', { mode: 'navigate' }), ORIGIN)).toBe('shell');
  });

  it('treats hashed build files as cacheable assets', () => {
    expect(r.classifyRequest(req('/assets/index-abc123.js'), ORIGIN)).toBe('asset');
    expect(r.classifyRequest(req('/assets/index-abc123.css'), ORIGIN)).toBe('asset');
    expect(r.classifyRequest(req('/assets/space-grotesk-latin-400-normal-xyz.woff2'), ORIGIN)).toBe('asset');
  });

  it('never intercepts API calls — live data, auth, version check and SSE always hit the network', () => {
    expect(r.classifyRequest(req('/api/auth/me'), ORIGIN)).toBe('passthrough');
    expect(r.classifyRequest(req('/api/version'), ORIGIN)).toBe('passthrough');
    expect(r.classifyRequest(req('/api/cfb/live-stream'), ORIGIN)).toBe('passthrough');
    expect(r.classifyRequest(req('/api/espn/scores', { mode: 'navigate' }), ORIGIN)).toBe('passthrough');
  });

  it('leaves everything else alone', () => {
    expect(r.classifyRequest(req('/sw.js'), ORIGIN)).toBe('passthrough');
    expect(r.classifyRequest(req('/manifest.json'), ORIGIN)).toBe('passthrough');
    expect(r.classifyRequest(req('/Images/hero.jpg'), ORIGIN)).toBe('passthrough');
    expect(r.classifyRequest(req('/_vercel/insights/script.js'), ORIGIN)).toBe('passthrough');
    expect(r.classifyRequest(req('/assets/x.js', { origin: 'https://cdn.example.com' }), ORIGIN)).toBe('passthrough');
    expect(r.classifyRequest(req('/assets/x.js', { method: 'POST' }), ORIGIN)).toBe('passthrough');
    expect(r.classifyRequest(req('/', { mode: 'navigate', method: 'POST' }), ORIGIN)).toBe('passthrough');
  });
});

describe('isCacheableResponse', () => {
  it('caches a plain 200 HTML page as the shell', () => {
    expect(r.isCacheableResponse(res(), 'shell')).toBe(true);
  });

  it('refuses redirected responses — Safari rejects them when served for a navigation', () => {
    expect(r.isCacheableResponse(res({ redirected: true }), 'shell')).toBe(false);
    expect(r.isCacheableResponse(res({ redirected: true, contentType: 'text/javascript' }), 'asset')).toBe(false);
  });

  it('refuses errors, opaque responses, and a non-HTML shell', () => {
    expect(r.isCacheableResponse(res({ status: 404 }), 'shell')).toBe(false);
    expect(r.isCacheableResponse(res({ status: 500, contentType: 'text/javascript' }), 'asset')).toBe(false);
    expect(r.isCacheableResponse(res({ type: 'opaque' }), 'shell')).toBe(false);
    expect(r.isCacheableResponse(res({ contentType: 'application/json' }), 'shell')).toBe(false);
  });

  it('refuses an HTML page served in place of a missing asset (the SPA rewrite fallback)', () => {
    expect(r.isCacheableResponse(res({ contentType: 'text/html' }), 'asset')).toBe(false);
    expect(r.isCacheableResponse(res({ contentType: 'text/javascript' }), 'asset')).toBe(true);
  });
});

describe('assetUrlsInShell', () => {
  // The launch that installs the worker already downloaded the app's code before the worker was
  // in control, so those files never passed through it — save the ones the page itself references,
  // or the first offline launch would get the page but none of the code to run it.
  it('lists the build files the page references, once each', () => {
    const html = `<script type="module" crossorigin src="/assets/index-AbC123.js"></script>
      <link rel="modulepreload" crossorigin href="/assets/vendor-x_9.js">
      <link rel="stylesheet" crossorigin href="/assets/index-Def456.css">
      <script src="/assets/index-AbC123.js"></script>
      <link rel="icon" href="/favicon.ico">`;
    expect(r.assetUrlsInShell(html)).toEqual(['/assets/index-AbC123.js', '/assets/vendor-x_9.js', '/assets/index-Def456.css']);
  });
});

describe('cache housekeeping', () => {
  it('deletes only older versions of our own caches', () => {
    expect(r.cachesToDelete([r.SHELL_CACHE, r.ASSET_CACHE, 'ivl-shell-v0', 'ivl-assets-v0', 'someone-else'])).toEqual([
      'ivl-shell-v0',
      'ivl-assets-v0',
    ]);
  });

  it('evicts the oldest asset entries once over the limit', () => {
    expect(r.assetsToEvict(['/assets/a', '/assets/b', '/assets/c', '/assets/d'], 2, [])).toEqual(['/assets/a', '/assets/b']);
    expect(r.assetsToEvict(['/assets/a', '/assets/b'], 2, [])).toEqual([]);
  });

  // A file whose hash survives many deploys (e.g. a vendor chunk) stays at the front of the cache
  // in insertion order — evicting it by age alone would leave the saved page unable to boot offline.
  it('never evicts a file the saved page still references, however old', () => {
    const cached = ['/assets/vendor', '/assets/old1', '/assets/old2', '/assets/index-new'];
    expect(r.assetsToEvict(cached, 2, ['/assets/vendor', '/assets/index-new'])).toEqual(['/assets/old1', '/assets/old2']);
  });

  it('keeps every referenced file even when they alone exceed the limit', () => {
    expect(r.assetsToEvict(['/assets/a', '/assets/b', '/assets/c'], 1, ['/assets/a', '/assets/b'])).toEqual(['/assets/c']);
  });

  it('gives the network a few seconds before falling back to the saved shell', () => {
    expect(r.NAVIGATION_TIMEOUT_MS).toBeGreaterThanOrEqual(2000);
    expect(r.NAVIGATION_TIMEOUT_MS).toBeLessThanOrEqual(5000);
  });
});

// The production off-switch: one flag flip must stop all caching and clear what's there.
describe('kill switch (CACHE_ENABLED = false)', () => {
  const src = swRoutingSrc.replace('var CACHE_ENABLED = true;', 'var CACHE_ENABLED = false;');
  const off = loadRouting(src);

  it('is a real flag in the shipped file', () => {
    expect(src).not.toBe(swRoutingSrc);
  });

  it('exposes the flag so the worker can also turn off navigation preload', () => {
    expect(r.CACHE_ENABLED).toBe(true);
    expect(off.CACHE_ENABLED).toBe(false);
  });

  it('lets every request go straight to the network', () => {
    expect(off.classifyRequest(req('/', { mode: 'navigate' }), ORIGIN)).toBe('passthrough');
    expect(off.classifyRequest(req('/assets/index-abc123.js'), ORIGIN)).toBe('passthrough');
  });

  it('deletes all of our caches, current versions included', () => {
    expect(off.cachesToDelete([off.SHELL_CACHE, off.ASSET_CACHE, 'someone-else'])).toEqual([off.SHELL_CACHE, off.ASSET_CACHE]);
  });
});
