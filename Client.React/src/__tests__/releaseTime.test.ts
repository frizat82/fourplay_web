import { formatReleaseWhen, formatTimeUntil } from '../utils/releaseTime';

// Dates are built with the local-time constructor so these hold in any test-runner time zone —
// the component shows the release in the viewer's own local time.
const now = new Date(2026, 9, 19, 20, 0); // Mon Oct 19, 8:00 PM

describe('formatReleaseWhen', () => {
  it('says Today or Tomorrow when that is what it is', () => {
    expect(formatReleaseWhen(new Date(2026, 9, 19, 22, 30), now)).toBe('Today 10:30 PM');
    expect(formatReleaseWhen(new Date(2026, 9, 20, 9, 15), now)).toBe('Tomorrow 9:15 AM');
  });

  it('uses the weekday within the coming week', () => {
    expect(formatReleaseWhen(new Date(2026, 9, 22, 9, 15), now)).toBe('Thu 9:15 AM');
    expect(formatReleaseWhen(new Date(2026, 9, 25, 9, 15), now)).toBe('Sun 9:15 AM');
  });

  // A week or more out, a bare weekday would be ambiguous — add the date.
  it('adds the date a week or more out', () => {
    expect(formatReleaseWhen(new Date(2026, 9, 26, 9, 15), now)).toBe('Mon, Oct 26 · 9:15 AM');
    expect(formatReleaseWhen(new Date(2026, 10, 12, 8, 0), now)).toBe('Thu, Nov 12 · 8:00 AM');
  });
});

describe('formatTimeUntil', () => {
  const s = 1000;
  const m = 60 * s;
  const h = 60 * m;
  const d = 24 * h;

  // Over an hour out: no seconds — they'd only sit frozen between once-a-minute updates.
  it('shows days and hours when a day or more out', () => {
    expect(formatTimeUntil(2 * d + 13 * h + 24 * m + 7 * s)).toBe('in 2 days, 13 hours');
    expect(formatTimeUntil(1 * d + 1 * h)).toBe('in 1 day, 1 hour');
    expect(formatTimeUntil(3 * d + 20 * m)).toBe('in 3 days');
  });

  it('shows hours and minutes under a day', () => {
    expect(formatTimeUntil(13 * h + 24 * m + 59 * s)).toBe('in 13 hours, 24 min');
    expect(formatTimeUntil(1 * h + 1 * m)).toBe('in 1 hour, 1 min');
    expect(formatTimeUntil(2 * h + 30 * s)).toBe('in 2 hours');
  });

  // Inside the last hour it's a live clock.
  it('counts down mm:ss inside the last hour', () => {
    expect(formatTimeUntil(h - 1)).toBe('in 59:59');
    expect(formatTimeUntil(42 * m + 7 * s)).toBe('in 42:07');
    expect(formatTimeUntil(5 * s)).toBe('in 00:05');
  });

  it('says it is posting once the time has come', () => {
    expect(formatTimeUntil(0)).toBe('Posting now');
    expect(formatTimeUntil(-5 * m)).toBe('Posting now');
  });
});
