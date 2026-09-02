export function buildAbsoluteUrl(path: string): string {
  return new URL(path, window.location.origin).toString();
}

// Shared by every unauthenticated redirect-to-login site (RequireAuth, RequireAdmin,
// JoinLeaguePage's log-in option) so the returnUrl param stays consistent everywhere.
export function buildLoginUrl(returnPath: string): string {
  return `/account/login?returnUrl=${encodeURIComponent(returnPath)}`;
}
