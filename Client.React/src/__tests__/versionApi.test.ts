import { vi } from 'vitest';
import { getDeployedVersion } from '../api/version';

// The update banner compares this build against the website's own deployed build — never the
// backend's (/api/version), which deploys separately: whenever the two differ (a frontend-only
// release, a backend deploy lagging or paused), Refresh reloaded the same page and the banner
// came straight back.
describe('getDeployedVersion', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  const respond = (body: string, status = 200) => vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(body, { status })));

  it("reads the website's own version.json, bypassing the HTTP cache, with a timeout", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ sha: 'abc123' }), { status: 200 }));
    vi.stubGlobal('fetch', fetchMock);

    await expect(getDeployedVersion()).resolves.toEqual({ sha: 'abc123' });
    expect(fetchMock).toHaveBeenCalledWith('/version.json', expect.objectContaining({ cache: 'no-store', signal: expect.any(AbortSignal) }));
  });

  // Any of these must fail the check (no banner), never read as "a different version" — that
  // would latch the banner on for good, the very bug this guards against.
  it('fails when version.json is missing', async () => {
    respond('not found', 404);
    await expect(getDeployedVersion()).rejects.toThrow();
  });

  it('fails when the SPA rewrite answers with the HTML page instead', async () => {
    respond('<!doctype html><html></html>');
    await expect(getDeployedVersion()).rejects.toThrow();
  });

  it.each(['{}', '{"sha":null}', '{"sha":""}', '[]'])('fails on a body without a sha: %s', async (body) => {
    respond(body);
    await expect(getDeployedVersion()).rejects.toThrow();
  });
});
