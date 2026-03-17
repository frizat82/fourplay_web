export function decodeBase64Url(input: string): string {
  const normalized = input.replace(/-/g, '+').replace(/_/g, '/');
  const pad = normalized.length % 4 === 0 ? '' : '='.repeat(4 - (normalized.length % 4));
  const base64 = normalized + pad;
  const decoded = atob(base64);
  return decoded;
}

export function isValidBase64Url(input: string): boolean {
  try {
    decodeBase64Url(input);
    return true;
  } catch {
    return false;
  }
}
