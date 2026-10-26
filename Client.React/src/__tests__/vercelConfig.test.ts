import vercel from '../../vercel.json';

const cacheControlFor = (source: string) =>
  vercel.headers.find(h => h.source === source)?.headers.find(h => h.key === 'Cache-Control')?.value;

describe('vercel.json caching', () => {
  // Team logos and weather icons were served "max-age=0, must-revalidate" — every launch re-checked
  // ~28+ logos over cellular, and one failed request left a team showing its text badge. Cache them
  // a week; a file replaced in place (e.g. a rebrand) still reaches phones via background revalidation.
  it('caches /Icons/* in the browser instead of re-checking every launch', () => {
    const value = cacheControlFor('/Icons/(.*)');
    expect(Number(value?.match(/max-age=(\d+)/)?.[1])).toBeGreaterThanOrEqual(86400);
    expect(value).toContain('stale-while-revalidate');
  });

  it('keeps the service worker and version file uncached', () => {
    expect(cacheControlFor('/sw.js')).toBe('no-cache');
    expect(cacheControlFor('/version.json')).toBe('no-store');
  });
});
