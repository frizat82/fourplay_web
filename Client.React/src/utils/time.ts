const CST_TZ = 'America/Chicago';

function getCstParts(date: Date) {
  const formatter = new Intl.DateTimeFormat('en-US', {
    timeZone: CST_TZ,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hour12: false,
  });
  const parts = formatter.formatToParts(date);
  const lookup: Record<string, string> = {};
  for (const part of parts) {
    if (part.type !== 'literal') {
      lookup[part.type] = part.value;
    }
  }
  return lookup;
}

export function getCstDate(date: Date = new Date()): Date {
  const parts = getCstParts(date);
  const iso = `${parts.year}-${parts.month}-${parts.day}T${parts.hour}:${parts.minute}:${parts.second}`;
  return new Date(iso);
}

export function toCstDisplay(dateIso: string, format: Intl.DateTimeFormatOptions = {}) {
  const date = new Date(dateIso);
  const formatter = new Intl.DateTimeFormat('en-US', {
    timeZone: CST_TZ,
    ...format,
  });
  return formatter.format(date);
}

export function isPastNoonCst(now: Date = new Date()): boolean {
  const nowCst = getCstDate(now);
  const day = nowCst.getDay();
  const sunday = new Date(nowCst);
  sunday.setDate(nowCst.getDate() - day);
  sunday.setHours(12, 0, 0, 0);
  const monday = new Date(sunday);
  monday.setDate(sunday.getDate() + 1);
  monday.setHours(23, 50, 0, 0);
  return nowCst >= sunday && nowCst <= monday;
}

export function untilNoonCst(now: Date = new Date()): number | null {
  const nowCst = getCstDate(now);
  const day = nowCst.getDay();
  const sunday = new Date(nowCst);
  sunday.setDate(nowCst.getDate() + ((7 - day) % 7));
  sunday.setHours(12, 0, 0, 0);
  if (nowCst >= sunday) {
    return null;
  }
  return sunday.getTime() - nowCst.getTime();
}

export function daysHoursMinutesUntilNoonCst(now: Date = new Date()): string | null {
  const diff = untilNoonCst(now);
  if (diff === null) return null;
  const totalMinutes = Math.floor(diff / 60000);
  const days = Math.floor(totalMinutes / (60 * 24));
  const hours = Math.floor((totalMinutes % (60 * 24)) / 60);
  const minutes = totalMinutes % 60;
  return `${days}d ${hours}h ${minutes}m`;
}
