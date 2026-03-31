/**
 * Tests for src/utils/time.ts
 *
 * All test dates use November (CST, UTC-6) to avoid DST ambiguity.
 * 2024-11-10 is a Sunday.
 */
import { getCstDate } from '../utils/time';

describe('getCstDate', () => {
  it('returns a Date object', () => {
    const result = getCstDate(new Date('2024-11-10T18:00:00Z'));
    expect(result).toBeInstanceOf(Date);
  });

  it('is deterministic — same input produces same output', () => {
    const input = new Date('2024-11-10T18:00:00Z');
    const a = getCstDate(input);
    const b = getCstDate(input);
    expect(a.getTime()).toBe(b.getTime());
  });

  it('converts a UTC noon Sunday to CST 6:00 AM (UTC-6)', () => {
    // 2024-11-10T12:00:00Z = 6:00 AM CST
    const result = getCstDate(new Date('2024-11-10T12:00:00Z'));
    expect(result.getHours()).toBe(6);
    expect(result.getMinutes()).toBe(0);
  });

  it('converts 18:00 UTC to noon (12:00) CST', () => {
    // 2024-11-10T18:00:00Z = 12:00 PM CST
    const result = getCstDate(new Date('2024-11-10T18:00:00Z'));
    expect(result.getHours()).toBe(12);
    expect(result.getMinutes()).toBe(0);
  });
});

