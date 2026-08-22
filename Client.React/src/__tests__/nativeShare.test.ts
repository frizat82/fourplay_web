import { vi } from 'vitest';
import { shareViaNavigator } from '../utils/nativeShare';

describe('shareViaNavigator', () => {
  const originalShare = navigator.share;

  afterEach(() => {
    Object.defineProperty(navigator, 'share', { value: originalShare, configurable: true });
  });

  it('calls navigator.share with the given data', () => {
    const shareMock = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'share', { value: shareMock, configurable: true });

    shareViaNavigator({ title: 'Hello', url: 'https://example.com' });

    expect(shareMock).toHaveBeenCalledWith({ title: 'Hello', url: 'https://example.com' });
  });

  it('does not throw an unhandled rejection when the user cancels the native share sheet', async () => {
    Object.defineProperty(navigator, 'share', {
      value: vi.fn().mockRejectedValue(new DOMException('Abort', 'AbortError')),
      configurable: true,
    });
    const onUnhandledRejection = vi.fn();
    window.addEventListener('unhandledrejection', onUnhandledRejection);

    shareViaNavigator({ title: 'Hello', url: 'https://example.com' });
    await new Promise((resolve) => setTimeout(resolve, 0));

    window.removeEventListener('unhandledrejection', onUnhandledRejection);
    expect(onUnhandledRejection).not.toHaveBeenCalled();
  });
});
