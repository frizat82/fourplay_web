/**
 * Tests for src/utils/auth.ts
 *
 * isAdmin(user) checks whether user.claims contains a claim whose type
 * includes 'role' (case-insensitive) and whose value is 'administrator'
 * (case-insensitive).
 */
import { isAdmin } from '../utils/auth';
import type { UserInfo } from '../types/auth';

// Helper to build a minimal UserInfo
function makeUser(claims: { type: string; value: string }[]): UserInfo {
  return { userId: '1', name: 'TestUser', claims };
}

describe('isAdmin', () => {
  it('returns false for null user', () => {
    expect(isAdmin(null)).toBe(false);
  });

  it('returns false for user with empty claims array', () => {
    expect(isAdmin(makeUser([]))).toBe(false);
  });

  it('returns false for user with unrelated claims (no role claim)', () => {
    const user = makeUser([
      { type: 'email', value: 'test@example.com' },
      { type: 'name', value: 'Test User' },
    ]);
    expect(isAdmin(user)).toBe(false);
  });

  it('returns true for user with Administrator role claim', () => {
    // The claim type just needs to include 'role' (case-insensitive);
    // the standard ASP.NET Core role claim type used by Identity is the full URI,
    // but isAdmin only requires .includes('role'), so we use the short form here.
    const user = makeUser([
      { type: 'role', value: 'Administrator' },
    ]);
    expect(isAdmin(user)).toBe(true);
  });

  it('returns true when role claim type contains "role" in a longer URI', () => {
    // ASP.NET Core Identity emits the full claim type URI in JWT tokens
    const user = makeUser([
      {
        type: 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role',
        value: 'Administrator',
      },
    ]);
    expect(isAdmin(user)).toBe(true);
  });

  it('returns true for Administrator value regardless of case', () => {
    const user = makeUser([{ type: 'role', value: 'administrator' }]);
    expect(isAdmin(user)).toBe(true);
  });

  it('returns false for user with non-admin role claim (User role)', () => {
    const user = makeUser([{ type: 'role', value: 'User' }]);
    expect(isAdmin(user)).toBe(false);
  });

  it('returns false for user with non-admin role claim (Member role)', () => {
    const user = makeUser([{ type: 'role', value: 'Member' }]);
    expect(isAdmin(user)).toBe(false);
  });

  it('returns true when Administrator claim is among multiple claims', () => {
    const user = makeUser([
      { type: 'email', value: 'admin@example.com' },
      { type: 'role', value: 'Administrator' },
      { type: 'name', value: 'Admin User' },
    ]);
    expect(isAdmin(user)).toBe(true);
  });

  it('returns false when user object has undefined claims (defensive)', () => {
    // Cast to simulate a broken/partial API response
    const user = { userId: '1', name: 'Bad', claims: undefined } as unknown as UserInfo;
    expect(isAdmin(user)).toBe(false);
  });
});
