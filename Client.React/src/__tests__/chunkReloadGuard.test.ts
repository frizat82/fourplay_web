import { vi } from 'vitest';
import { handleChunkLoadError } from '../utils/chunkReloadGuard';

// Route-level code splitting means a tab left open across a deploy can try to import a hashed
// chunk the new deployment no longer serves. Vite reports that as `vite:preloadError`; the fix
// is one reload onto the new build — but never a reload loop if the chunk is genuinely broken.
describe('handleChunkLoadError', () => {
  beforeEach(() => sessionStorage.clear());

  it('reloads once and suppresses the error on the first failure', () => {
    const reload = vi.fn();
    const event = { preventDefault: vi.fn() };

    handleChunkLoadError(event, reload, 1_000_000);

    expect(reload).toHaveBeenCalledTimes(1);
    expect(event.preventDefault).toHaveBeenCalled();
  });

  it('does not reload again when the previous reload was moments ago (no loop)', () => {
    const reload = vi.fn();
    handleChunkLoadError({ preventDefault: vi.fn() }, reload, 1_000_000);
    reload.mockClear();

    const event = { preventDefault: vi.fn() };
    handleChunkLoadError(event, reload, 1_005_000);

    expect(reload).not.toHaveBeenCalled();
    // Let the error surface normally instead of swallowing it.
    expect(event.preventDefault).not.toHaveBeenCalled();
  });

  it('reloads again for a later deploy once the guard window has passed', () => {
    const reload = vi.fn();
    handleChunkLoadError({ preventDefault: vi.fn() }, reload, 1_000_000);
    reload.mockClear();

    handleChunkLoadError({ preventDefault: vi.fn() }, reload, 1_000_000 + 60_000);

    expect(reload).toHaveBeenCalledTimes(1);
  });
});
