import { vi } from 'vitest';
import { RESUME_RELOAD_AFTER_MS, createResumeReloadHandler } from '../utils/resumeReload';

// An installed iOS home-screen app that sits in the background for a long time can come back to
// an all-white screen (WebKit discards the page's rendering but resumes the suspended JS), which
// only a force-quit recovered. Reloading on resume after a long background stint does the same
// thing automatically; a short app switch must not reload (it would drop in-progress picks).
describe('createResumeReloadHandler', () => {
  function setup() {
    const reload = vi.fn();
    let now = 1_000_000;
    const handler = createResumeReloadHandler(reload, () => now);
    return { reload, handler, advance: (ms: number) => { now += ms; } };
  }

  it('reloads when the app becomes visible after being hidden past the threshold', () => {
    const { reload, handler, advance } = setup();
    handler('hidden');
    advance(RESUME_RELOAD_AFTER_MS);
    handler('visible');
    expect(reload).toHaveBeenCalledTimes(1);
  });

  it('does not reload after a short trip to the background', () => {
    const { reload, handler, advance } = setup();
    handler('hidden');
    advance(RESUME_RELOAD_AFTER_MS - 1);
    handler('visible');
    expect(reload).not.toHaveBeenCalled();
  });

  it('does not reload on a visible event that was never preceded by hidden', () => {
    const { reload, handler, advance } = setup();
    advance(RESUME_RELOAD_AFTER_MS * 10);
    handler('visible');
    expect(reload).not.toHaveBeenCalled();
  });

  it('measures each background stint separately rather than accumulating', () => {
    const { reload, handler, advance } = setup();
    handler('hidden');
    advance(RESUME_RELOAD_AFTER_MS - 1);
    handler('visible');
    advance(RESUME_RELOAD_AFTER_MS * 5); // in the foreground — doesn't count
    handler('hidden');
    advance(1000);
    handler('visible');
    expect(reload).not.toHaveBeenCalled();
  });

  it('keeps the earliest hidden time when hidden fires twice (visibilitychange + pagehide)', () => {
    const { reload, handler, advance } = setup();
    handler('hidden');
    advance(RESUME_RELOAD_AFTER_MS - 1000);
    handler('hidden');
    advance(1000);
    handler('visible');
    expect(reload).toHaveBeenCalledTimes(1);
  });
});
