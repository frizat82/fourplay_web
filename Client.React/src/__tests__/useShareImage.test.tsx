import { renderHook, waitFor } from '@testing-library/react';
import { vi } from 'vitest';
import { useShareImage } from '../utils/useShareImage';

const toastPush = vi.fn();
vi.mock('../services/toast', () => ({ useToast: () => ({ push: toastPush }) }));

const mockedToBlob = vi.fn();
vi.mock('html-to-image', () => ({ toBlob: (...args: unknown[]) => mockedToBlob(...args) }));

describe('useShareImage', () => {
  const originalShare = navigator.share;
  const originalCanShare = navigator.canShare;
  const fakeBlob = new Blob(['fake-png-bytes'], { type: 'image/png' });
  const node = document.createElement('div');

  beforeEach(() => {
    vi.clearAllMocks();
    mockedToBlob.mockResolvedValue(fakeBlob);
  });

  afterEach(() => {
    Object.defineProperty(navigator, 'share', { value: originalShare, configurable: true });
    Object.defineProperty(navigator, 'canShare', { value: originalCanShare, configurable: true });
  });

  it('shares the rendered node as a PNG file when the browser supports file sharing', async () => {
    const shareMock = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'canShare', { value: () => true, configurable: true });
    Object.defineProperty(navigator, 'share', { value: shareMock, configurable: true });

    const { result } = renderHook(() => useShareImage());
    await result.current.shareImage(node, 'My Standings', 'standings.png');

    await waitFor(() => expect(shareMock).toHaveBeenCalled());
    const call = shareMock.mock.calls[0][0];
    expect(call.title).toBe('My Standings');
    expect(call.files).toHaveLength(1);
    expect(call.files[0].name).toBe('standings.png');
    expect(call.files[0].type).toBe('image/png');
  });

  it('does not throw an unhandled rejection when the user cancels the native share sheet', async () => {
    Object.defineProperty(navigator, 'canShare', { value: () => true, configurable: true });
    Object.defineProperty(navigator, 'share', {
      value: vi.fn().mockRejectedValue(new DOMException('Abort', 'AbortError')),
      configurable: true,
    });
    const onUnhandledRejection = vi.fn();
    window.addEventListener('unhandledrejection', onUnhandledRejection);

    const { result } = renderHook(() => useShareImage());
    await result.current.shareImage(node, 'My Standings', 'standings.png');
    await new Promise((resolve) => setTimeout(resolve, 0));

    window.removeEventListener('unhandledrejection', onUnhandledRejection);
    expect(onUnhandledRejection).not.toHaveBeenCalled();
  });

  it.each([
    ['browser reports it cannot share files', () => false],
    ['navigator.canShare is entirely unavailable (older browsers)', undefined],
  ])('falls back to downloading the image when %s', async (_label, canShare) => {
    Object.defineProperty(navigator, 'canShare', { value: canShare, configurable: true });
    Object.defineProperty(navigator, 'share', { value: undefined, configurable: true });
    const createObjectURL = vi.fn().mockReturnValue('blob:fake-url');
    const revokeObjectURL = vi.fn();
    Object.defineProperty(URL, 'createObjectURL', { value: createObjectURL, configurable: true });
    Object.defineProperty(URL, 'revokeObjectURL', { value: revokeObjectURL, configurable: true });
    const clickSpy = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});

    const { result } = renderHook(() => useShareImage());
    await result.current.shareImage(node, 'My Standings', 'standings.png');

    expect(createObjectURL).toHaveBeenCalledWith(fakeBlob);
    expect(clickSpy).toHaveBeenCalled();
    expect(toastPush).toHaveBeenCalledWith(expect.stringMatching(/downloaded/i), 'info');
    // Revoke is deliberately deferred (see useShareImage.ts) so the browser has time to start
    // reading the blob before the URL is invalidated.
    await waitFor(() => expect(revokeObjectURL).toHaveBeenCalledWith('blob:fake-url'), { timeout: 1500 });

    clickSpy.mockRestore();
  });

  it('shows an error toast and does not throw when image generation fails', async () => {
    mockedToBlob.mockResolvedValue(null);

    const { result } = renderHook(() => useShareImage());
    await result.current.shareImage(node, 'My Standings', 'standings.png');

    expect(toastPush).toHaveBeenCalledWith(expect.stringMatching(/failed/i), 'error');
  });

  it('shows an error toast and does not throw when toBlob rejects (e.g. a blocked font fetch)', async () => {
    mockedToBlob.mockRejectedValue(new Error('network error'));

    const { result } = renderHook(() => useShareImage());
    await result.current.shareImage(node, 'My Standings', 'standings.png');

    expect(toastPush).toHaveBeenCalledWith(expect.stringMatching(/failed/i), 'error');
  });

  it('shows an error toast and does not throw when the node is not mounted yet', async () => {
    const { result } = renderHook(() => useShareImage());
    await result.current.shareImage(null, 'My Standings', 'standings.png');

    expect(toastPush).toHaveBeenCalledWith(expect.stringMatching(/nothing to share/i), 'error');
    expect(mockedToBlob).not.toHaveBeenCalled();
  });
});
