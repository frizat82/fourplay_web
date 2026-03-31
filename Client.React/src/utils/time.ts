export function toLocalDisplay(dateIso: string, format: Intl.DateTimeFormatOptions = {}) {
  return new Intl.DateTimeFormat('en-US', format).format(new Date(dateIso));
}
