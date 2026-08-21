import { renderHook, waitFor } from '@testing-library/react';
import { vi } from 'vitest';
import { useShareLink } from '../utils/useShareLink';

const toastPush = vi.fn();
vi.mock('../services/toast', () => ({ useToast: () => ({ push: toastPush }) }));

describe('useShareLink', () => {
  const originalShare = navigator.share;
  const originalClipboard = navigator.clipboard;

  beforeEach(() => {
    vi.clearAllMocks();
    Object.defineProperty(navigator, 'clipboard', {
      value: { writeText: vi.fn().mockResolvedValue(undefined) },
      configurable: true,
    });
  });

  afterEach(() => {
    Object.defineProperty(navigator, 'share', { value: originalShare, configurable: true });
    Object.defineProperty(navigator, 'clipboard', { value: originalClipboard, configurable: true });
  });

  it('copy() writes the url to the clipboard and shows a success toast', async () => {
    const { result } = renderHook(() => useShareLink());
    await result.current.copy('https://ivleague.com/scores');
    expect(navigator.clipboard.writeText).toHaveBeenCalledWith('https://ivleague.com/scores');
    expect(toastPush).toHaveBeenCalledWith('Link copied', 'info');
  });

  it('copy() shows an error toast when the clipboard write fails', async () => {
    Object.defineProperty(navigator, 'clipboard', {
      value: { writeText: vi.fn().mockRejectedValue(new Error('denied')) },
      configurable: true,
    });
    const { result } = renderHook(() => useShareLink());
    await result.current.copy('https://ivleague.com/scores');
    expect(toastPush).toHaveBeenCalledWith('Failed to copy link', 'error');
  });

  it('share() calls navigator.share with title and url when available', async () => {
    const shareMock = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'share', { value: shareMock, configurable: true });

    const { result } = renderHook(() => useShareLink());
    result.current.share('Week 5 scores', 'https://ivleague.com/scores');

    await waitFor(() => expect(shareMock).toHaveBeenCalledWith({ title: 'Week 5 scores', url: 'https://ivleague.com/scores' }));
    expect(navigator.clipboard.writeText).not.toHaveBeenCalled();
  });

  it('share() falls back to copy() when navigator.share is unavailable', async () => {
    Object.defineProperty(navigator, 'share', { value: undefined, configurable: true });

    const { result } = renderHook(() => useShareLink());
    result.current.share('Week 5 scores', 'https://ivleague.com/scores');

    await waitFor(() => expect(navigator.clipboard.writeText).toHaveBeenCalledWith('https://ivleague.com/scores'));
    expect(toastPush).toHaveBeenCalledWith('Link copied', 'info');
  });
});
