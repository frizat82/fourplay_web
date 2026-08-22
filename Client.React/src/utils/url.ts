export function buildAbsoluteUrl(path: string): string {
  return new URL(path, window.location.origin).toString();
}
