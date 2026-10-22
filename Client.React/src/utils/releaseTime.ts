// Formatting for the "when do spreads drop" card (SpreadRelease). Both take the time explicitly
// so they're pure and testable; both use the viewer's own local time.

const DAY_MS = 24 * 60 * 60 * 1000;
const HOUR_MS = 60 * 60 * 1000;
const MINUTE_MS = 60 * 1000;

function startOfDay(date: Date) {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate()).getTime();
}

/**
 * When the release is, in words people plan around: "Today 10:30 PM", "Tomorrow 9:15 AM",
 * "Thu 9:15 AM" within the coming week, else "Thu, Oct 29 · 9:15 AM".
 */
export function formatReleaseWhen(target: Date, now: Date): string {
  const time = target.toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit' });
  const daysAway = Math.round((startOfDay(target) - startOfDay(now)) / DAY_MS);
  if (daysAway === 0) return `Today ${time}`;
  if (daysAway === 1) return `Tomorrow ${time}`;
  const weekday = target.toLocaleDateString('en-US', { weekday: 'short' });
  if (daysAway > 1 && daysAway < 7) return `${weekday} ${time}`;
  const date = target.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
  return `${weekday}, ${date} · ${time}`;
}

const plural = (n: number, unit: string) => `${n} ${unit}${n === 1 ? '' : 's'}`;

/**
 * How long until the release. Over an hour out it's coarse ("in 2 days, 13 hours", "in 13 hours,
 * 24 min") — no seconds, which would only sit frozen between once-a-minute updates. Inside the
 * last hour it's a live clock ("in 42:07").
 */
export function formatTimeUntil(diffMs: number): string {
  if (diffMs <= 0) return 'Posting now';
  if (diffMs < HOUR_MS) {
    const totalSeconds = Math.floor(diffMs / 1000);
    const pad = (n: number) => n.toString().padStart(2, '0');
    return `in ${pad(Math.floor(totalSeconds / 60))}:${pad(totalSeconds % 60)}`;
  }
  const days = Math.floor(diffMs / DAY_MS);
  const hours = Math.floor((diffMs % DAY_MS) / HOUR_MS);
  if (days > 0) return hours > 0 ? `in ${plural(days, 'day')}, ${plural(hours, 'hour')}` : `in ${plural(days, 'day')}`;
  const minutes = Math.floor((diffMs % HOUR_MS) / MINUTE_MS);
  return minutes > 0 ? `in ${plural(hours, 'hour')}, ${minutes} min` : `in ${plural(hours, 'hour')}`;
}
