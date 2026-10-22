import { vi } from 'vitest';
import { getDeployedVersion } from '../api/version';

// The update banner compares this build against the website's own deployed build — never the
// backend's (/api/version), which deploys separately: whenever the two differ (a frontend-only
// release, a backend deploy lagging or paused), Refresh reloaded the same page and the banner
// came straight back.
describe('getDeployedVersion', () => {
  it("reads the website's own version.json, bypassing the HTTP cache", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ sha: 'abc123' }), { status: 200 }));
    vi.stubGlobal('fetch', fetchMock);

    await expect(getDeployedVersion()).resolves.toEqual({ sha: 'abc123' });
    expect(fetchMock).toHaveBeenCalledWith('/version.json', { cache: 'no-store' });

    vi.unstubAllGlobals();
  });

  it('fails when version.json is missing, so no banner is shown', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('not found', { status: 404 })));

    await expect(getDeployedVersion()).rejects.toThrow();

    vi.unstubAllGlobals();
  });
});
