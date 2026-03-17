/**
 * Tests for src/utils/time.ts
 *
 * All test dates use November (CST, UTC-6) to avoid DST ambiguity.
 * 2024-11-10 is a Sunday.
 *   Sunday noon CST = 2024-11-10T18:00:00Z
 *   Sunday 11:59 AM CST = 2024-11-10T17:59:00Z
 *   Sunday 12:01 PM CST = 2024-11-10T18:01:00Z
 */
import { getCstDate, isPastNoonCst, untilNoonCst, daysHoursMinutesUntilNoonCst } from '../utils/time';

// --- Constants used across tests ---
// 2024-11-10 is a Sunday (day=0). All dates use CST (UTC-6).
const SUNDAY_BEFORE_NOON = new Date('2024-11-10T17:59:00Z'); // 11:59 AM CST Sunday
const SUNDAY_NOON         = new Date('2024-11-10T18:00:00Z'); // 12:00 PM CST Sunday (boundary)
const SUNDAY_AFTER_NOON  = new Date('2024-11-10T18:01:00Z'); // 12:01 PM CST Sunday

// Wednesday — far from Sunday noon window
const WEDNESDAY_MORNING  = new Date('2024-11-06T15:00:00Z'); // 9:00 AM CST Wednesday

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
    const result = getCstDate(SUNDAY_NOON);
    expect(result.getHours()).toBe(12);
    expect(result.getMinutes()).toBe(0);
  });
});

describe('isPastNoonCst', () => {
  // isPastNoonCst returns true when CST time is between Sunday 12:00 and Monday 23:50.

  it('returns false at 11:59 AM CST Sunday (before noon window)', () => {
    expect(isPastNoonCst(SUNDAY_BEFORE_NOON)).toBe(false);
  });

  it('returns true at 12:01 PM CST Sunday (past noon window start)', () => {
    expect(isPastNoonCst(SUNDAY_AFTER_NOON)).toBe(true);
  });

  it('returns true exactly at noon CST Sunday (boundary is inclusive)', () => {
    // sunday.setHours(12,0,0,0) == nowCst so nowCst >= sunday is true
    expect(isPastNoonCst(SUNDAY_NOON)).toBe(true);
  });

  it('returns false on a Wednesday morning (not in Sunday-Monday window)', () => {
    expect(isPastNoonCst(WEDNESDAY_MORNING)).toBe(false);
  });

  it('returns true on Monday morning CST (still in noon-window)', () => {
    // 2024-11-11 is the Monday after the Sunday.
    // Monday 9:00 AM CST = 2024-11-11T15:00:00Z
    const mondayMorning = new Date('2024-11-11T15:00:00Z');
    expect(isPastNoonCst(mondayMorning)).toBe(true);
  });
});

describe('untilNoonCst', () => {
  // untilNoonCst returns ms until the *next* (or same) Sunday noon CST,
  // or null if already past it (nowCst >= sunday noon).

  it('returns null when past noon CST Sunday', () => {
    expect(untilNoonCst(SUNDAY_AFTER_NOON)).toBeNull();
  });

  it('returns null exactly at noon CST Sunday', () => {
    // At exactly noon, nowCst >= sunday, so null
    expect(untilNoonCst(SUNDAY_NOON)).toBeNull();
  });

  it('returns a positive number before noon CST Sunday', () => {
    const result = untilNoonCst(SUNDAY_BEFORE_NOON);
    expect(result).not.toBeNull();
    expect(result!).toBeGreaterThan(0);
  });

  it('returns approximately 1 minute in ms when 1 minute before noon CST', () => {
    // 17:59 UTC → 11:59 AM CST. noon is 18:00 UTC → 1 min away.
    // getCstDate strips timezone offset, so the diff is in "local" ms.
    // The function computes: sunday.getTime() - nowCst.getTime()
    // sunday = nowCst day with hours set to 12:00:00.000
    // nowCst = getCstDate(SUNDAY_BEFORE_NOON) which has hours=11, minutes=59
    // diff = 60_000 ms (1 minute)
    const result = untilNoonCst(SUNDAY_BEFORE_NOON);
    expect(result).toBe(60_000);
  });

  it('returns approximately 6 hours in ms when 6 hours before noon CST', () => {
    // 6:00 AM CST Sunday = 12:00 UTC = 2024-11-10T12:00:00Z
    const sixHoursBefore = new Date('2024-11-10T12:00:00Z');
    const result = untilNoonCst(sixHoursBefore);
    expect(result).toBe(6 * 60 * 60 * 1000);
  });

  it('returns a value for Wednesday (next Sunday noon is 4 days away)', () => {
    // Wednesday 9:00 AM CST = 2024-11-06T15:00:00Z
    // Next Sunday = 2024-11-10, noon CST
    // getCstDate gives local Wednesday 9:00 AM; next Sunday noon local = 4d 3h away
    const result = untilNoonCst(WEDNESDAY_MORNING);
    expect(result).not.toBeNull();
    expect(result!).toBeGreaterThan(0);
  });
});

describe('daysHoursMinutesUntilNoonCst', () => {
  it('returns null when past noon CST Sunday', () => {
    expect(daysHoursMinutesUntilNoonCst(SUNDAY_AFTER_NOON)).toBeNull();
  });

  it('returns null exactly at noon CST Sunday', () => {
    expect(daysHoursMinutesUntilNoonCst(SUNDAY_NOON)).toBeNull();
  });

  it('returns a non-null string before noon CST Sunday', () => {
    const result = daysHoursMinutesUntilNoonCst(SUNDAY_BEFORE_NOON);
    expect(result).not.toBeNull();
    expect(typeof result).toBe('string');
  });

  it('returns "0d 0h 1m" when exactly 1 minute before noon CST', () => {
    // 1 min = 60_000 ms → totalMinutes=1, days=0, hours=0, minutes=1
    expect(daysHoursMinutesUntilNoonCst(SUNDAY_BEFORE_NOON)).toBe('0d 0h 1m');
  });

  it('returns "0d 6h 0m" when 6 hours before noon CST', () => {
    // 2024-11-10T12:00:00Z = 6:00 AM CST (6 hours before noon)
    const sixHoursBefore = new Date('2024-11-10T12:00:00Z');
    expect(daysHoursMinutesUntilNoonCst(sixHoursBefore)).toBe('0d 6h 0m');
  });

  it('formats string as "{d}d {h}h {m}m" pattern', () => {
    const result = daysHoursMinutesUntilNoonCst(WEDNESDAY_MORNING);
    expect(result).toMatch(/^\d+d \d+h \d+m$/);
  });

  it('returns "0d 1h 30m" when 90 minutes before noon CST', () => {
    // 90 min before noon CST Sunday = 16:30 UTC on 2024-11-10
    const ninetyMinBefore = new Date('2024-11-10T16:30:00Z');
    // getCstDate → 10:30 AM CST; sunday noon = 12:00 CST → diff = 90 min = 5_400_000 ms
    // totalMinutes=90, days=0, hours=1, minutes=30
    expect(daysHoursMinutesUntilNoonCst(ninetyMinBefore)).toBe('0d 1h 30m');
  });
});
