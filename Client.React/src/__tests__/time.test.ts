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
});
