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

