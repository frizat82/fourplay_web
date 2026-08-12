export function toLocalDisplay(dateIso: string, format: Intl.DateTimeFormatOptions = {}) {
  const date = new Date(dateIso);
  // Intl.DateTimeFormat.format throws RangeError on an Invalid Date, unlike
  // Date.prototype.toLocaleString — guard so a bad/unexpected value renders as
  // text instead of crashing the whole app (no error boundary wraps routes today).
  if (Number.isNaN(date.getTime())) return 'Invalid Date';
  return new Intl.DateTimeFormat('en-US', format).format(date);
}
