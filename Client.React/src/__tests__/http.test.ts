import { AxiosError, type AxiosAdapter, type InternalAxiosRequestConfig } from 'axios';
import { http } from '../api/http';
import { isNetworkError } from '../utils/apiError';

// Stand-in for the network: each request's final config is recorded and answered by `reply`.
let seen: InternalAxiosRequestConfig[] = [];
let reply: (config: InternalAxiosRequestConfig) => Promise<unknown>;
const originalAdapter = http.defaults.adapter;

const ok = (config: InternalAxiosRequestConfig) =>
  Promise.resolve({ data: {}, status: 200, statusText: 'OK', headers: {}, config });
const status401 = (config: InternalAxiosRequestConfig) =>
  Promise.reject(new AxiosError('Unauthorized', 'ERR_BAD_REQUEST', config, null, {
    status: 401, statusText: 'Unauthorized', data: null, headers: {}, config,
  }));
const timedOut = (config: InternalAxiosRequestConfig) =>
  Promise.reject(new AxiosError('timeout of 15000ms exceeded', AxiosError.ECONNABORTED, config));

beforeEach(() => {
  seen = [];
  reply = ok;
  http.defaults.adapter = (async (config: InternalAxiosRequestConfig) => {
    seen.push(config);
    return reply(config);
  }) as AxiosAdapter;
});
afterEach(() => {
  http.defaults.adapter = originalAdapter;
});

describe('http client timeouts', () => {
  // On a weak phone signal a read can sit on a dead connection for a minute or more; without a
  // timeout the app shows an endless blank loading state (the iOS "white screen" report).
  it('times out reads instead of waiting on a stalled connection forever', async () => {
    await http.get('/api/auth/me');
    expect(seen[0].timeout).toBeGreaterThan(0);
    expect(seen[0].timeout).toBeLessThanOrEqual(20_000);
  });

  // A write the server already completed must not be reported as failed just because the answer
  // was slow — the user would think their picks weren't saved and resubmit.
  it('never times out writes', async () => {
    await http.post('/api/league/picks', {});
    await http.put('/api/x', {});
    await http.delete('/api/x');
    expect(seen.map(c => c.timeout ?? 0)).toEqual([0, 0, 0]);
  });
});

describe('expired session on a dead connection', () => {
  it('reports a connection problem, not "signed out", when the token refresh never gets an answer', async () => {
    reply = config => (config.url?.includes('/api/auth/refresh') ? timedOut(config) : status401(config));
    const error = await http.get('/api/auth/me').catch((e: unknown) => e);
    expect(isNetworkError(error)).toBe(true);
  });

  it('still reports the 401 when the refresh is genuinely rejected', async () => {
    reply = status401;
    const error = await http.get('/api/auth/me').catch((e: unknown) => e);
    expect(isNetworkError(error)).toBe(false);
    expect((error as AxiosError).response?.status).toBe(401);
  });
});
