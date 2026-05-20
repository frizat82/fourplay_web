import { renderHook } from '@testing-library/react';
import { describe, it, expect, afterEach } from 'vitest';
import { SportsProvider, useSportContext } from '../services/sport';

function wrapper({ children }: { children: React.ReactNode }) {
  return <SportsProvider>{children}</SportsProvider>;
}

function setHostname(hostname: string) {
  Object.defineProperty(window, 'location', {
    value: { ...window.location, hostname },
    writable: true,
    configurable: true,
  });
}

describe('useSportContext', () => {
  afterEach(() => {
    setHostname('localhost');
  });

  it('returns CFB when hostname starts with cfb.', () => {
    setHostname('cfb.localhost');
    const { result } = renderHook(() => useSportContext(), { wrapper });
    expect(result.current.sport).toBe('CFB');
  });

  it('returns NFL for root domain', () => {
    setHostname('localhost');
    const { result } = renderHook(() => useSportContext(), { wrapper });
    expect(result.current.sport).toBe('NFL');
  });

  it('returns NFL for ivleague.app domain', () => {
    setHostname('ivleague.app');
    const { result } = renderHook(() => useSportContext(), { wrapper });
    expect(result.current.sport).toBe('NFL');
  });

  it('returns CFB for cfb.ivleague.app domain', () => {
    setHostname('cfb.ivleague.app');
    const { result } = renderHook(() => useSportContext(), { wrapper });
    expect(result.current.sport).toBe('CFB');
  });

  it('throws if used outside SportsProvider', () => {
    expect(() => renderHook(() => useSportContext())).toThrow(
      'useSportContext must be used within a SportsProvider'
    );
  });
});
