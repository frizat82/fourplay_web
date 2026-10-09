import { vi } from 'vitest';
import { installAppShellWorker } from '../utils/appShellWorker';

// The service worker is what lets the installed app open from a saved copy on a weak signal
// instead of a white screen. It used to be registered only from the Notifications settings page
// (for push), so most users never had one — it must be registered for everyone in production.
describe('installAppShellWorker', () => {
  function fakeEnv(opts: { supported?: boolean; loaded?: boolean } = {}) {
    const register = vi.fn().mockResolvedValue({});
    const listeners: Record<string, () => void> = {};
    const nav = opts.supported === false ? {} : { serviceWorker: { register } };
    const win = {
      document: { readyState: opts.loaded ? 'complete' : 'loading' },
      addEventListener: vi.fn((type: string, fn: () => void) => { listeners[type] = fn; }),
    };
    return { register, listeners, nav, win };
  }

  it('registers /sw.js once the page has loaded, in production', () => {
    const { register, listeners, nav, win } = fakeEnv();
    installAppShellWorker({ isProd: true, nav, win });
    expect(register).not.toHaveBeenCalled(); // don't compete with the first load for bandwidth
    listeners.load();
    expect(register).toHaveBeenCalledWith('/sw.js');
  });

  it('registers right away if the page already finished loading', () => {
    const { register, nav, win } = fakeEnv({ loaded: true });
    installAppShellWorker({ isProd: true, nav, win });
    expect(register).toHaveBeenCalledWith('/sw.js');
  });

  it('does nothing in development, where the dev server serves unbundled modules', () => {
    const { register, win, nav } = fakeEnv({ loaded: true });
    installAppShellWorker({ isProd: false, nav, win });
    expect(register).not.toHaveBeenCalled();
  });

  it('does nothing when the browser has no service worker support', () => {
    const { win, nav } = fakeEnv({ supported: false, loaded: true });
    expect(() => installAppShellWorker({ isProd: true, nav, win })).not.toThrow();
  });

  it('swallows a registration failure — the app works without the worker', async () => {
    const { register, nav, win } = fakeEnv({ loaded: true });
    register.mockRejectedValueOnce(new Error('insecure context'));
    installAppShellWorker({ isProd: true, nav, win });
    await Promise.resolve();
  });
});
