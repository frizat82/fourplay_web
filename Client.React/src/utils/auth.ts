import type { UserInfo } from '../types/auth';

export function isAdmin(user: UserInfo | null): boolean {
  if (!user?.claims) return false;
  return user.claims.some((claim) =>
    claim.type.toLowerCase().includes('role') && claim.value.toLowerCase() === 'administrator'
  );
}
