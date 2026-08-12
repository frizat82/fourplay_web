import { toLocalDisplay } from '../utils/time';

describe('toLocalDisplay', () => {
  it('returns a non-empty string for a valid ISO date', () => {
    const result = toLocalDisplay('2024-11-10T18:00:00Z');
    expect(typeof result).toBe('string');
    expect(result.length).toBeGreaterThan(0);
  });

  it('accepts format options', () => {
    const result = toLocalDisplay('2024-11-10T18:00:00Z', { weekday: 'short' });
    expect(typeof result).toBe('string');
    expect(result.length).toBeGreaterThan(0);
  });

  it('does not throw on an invalid/unexpected date string', () => {
    // Intl.DateTimeFormat.format throws RangeError on an Invalid Date (unlike
    // Date.prototype.toLocaleString) — regression guard so a bad API response
    // renders as text instead of crashing the whole app (no error boundary
    // wraps routes today, so an uncaught render-time throw unmounts everything).
    expect(() => toLocalDisplay('')).not.toThrow();
    expect(() => toLocalDisplay('not-a-date')).not.toThrow();
    expect(toLocalDisplay('not-a-date')).toBe('Invalid Date');
  });
});
