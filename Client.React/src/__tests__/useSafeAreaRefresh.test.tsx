import { renderHook } from '@testing-library/react';
import { useSafeAreaRefresh } from '../utils/useSafeAreaRefresh';

function setVisibilityState(state: DocumentVisibilityState) {
  Object.defineProperty(document, 'visibilityState', { value: state, configurable: true });
}

describe('useSafeAreaRefresh', () => {
  afterEach(() => {
    setVisibilityState('visible');
    document.documentElement.style.display = '';
  });

  it('forces a reflow (display toggled off, then restored) when the document becomes visible', () => {
    setVisibilityState('visible');
    renderHook(() => useSafeAreaRefresh());

    const seen: string[] = [];
    const originalDescriptor = Object.getOwnPropertyDescriptor(document.documentElement.style, 'display')
      ?? Object.getOwnPropertyDescriptor(CSSStyleDeclaration.prototype, 'display');
    let current = '';
    Object.defineProperty(document.documentElement.style, 'display', {
      configurable: true,
      get: () => current,
      set: (value: string) => { current = value; seen.push(value); },
    });

    document.dispatchEvent(new Event('visibilitychange'));

    expect(seen).toEqual(['none', '']);

    if (originalDescriptor) {
      Object.defineProperty(document.documentElement.style, 'display', originalDescriptor);
    }
  });

  it('does nothing when the document is not visible', () => {
    setVisibilityState('hidden');
    renderHook(() => useSafeAreaRefresh());

    const seen: string[] = [];
    const originalDescriptor = Object.getOwnPropertyDescriptor(document.documentElement.style, 'display')
      ?? Object.getOwnPropertyDescriptor(CSSStyleDeclaration.prototype, 'display');
    let current = '';
    Object.defineProperty(document.documentElement.style, 'display', {
      configurable: true,
      get: () => current,
      set: (value: string) => { current = value; seen.push(value); },
    });

    document.dispatchEvent(new Event('visibilitychange'));

    expect(seen).toEqual([]);

    if (originalDescriptor) {
      Object.defineProperty(document.documentElement.style, 'display', originalDescriptor);
    }
  });

  it('removes the listener on unmount', () => {
    const removeSpy = vi.spyOn(document, 'removeEventListener');
    const { unmount } = renderHook(() => useSafeAreaRefresh());
    unmount();

    expect(removeSpy).toHaveBeenCalledWith('visibilitychange', expect.any(Function));
    removeSpy.mockRestore();
  });
});
