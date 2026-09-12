using Microsoft.EntityFrameworkCore;

namespace FourPlayWebApp.Server.Services.Repositories;

// Shared by LeagueRepository (NFL) and CfbPicksRepository (CFB) — see CLAUDE.md's
// no-duplicated-sport-logic rule. A user's own pick-cap check-and-insert (and the matching
// remove) must be atomic: reading "how many picks does this user have for this week/slate" and
// then writing are two separate steps, and two concurrent requests (a double-click, or two open
// tabs) can both read the same pre-write count and both pass the cap check. pg_advisory_xact_lock
// serializes writers sharing the same lock key for the lifetime of one transaction — scoped to a
// single (user, league, season, week/slate), so it never contends across different users or weeks.
//
// Postgres-only (hashtext + pg_advisory_xact_lock have no SQLite equivalent) — any test exercising
// this must run against real Postgres via Testcontainers, per CLAUDE.md's EF/Npgsql provider gotcha.
public static class PickConcurrencyGuard {
    public static async Task<T> RunLockedAsync<T>(DbContext db, string lockKey, Func<Task<T>> body) {
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({lockKey}))");
        var result = await body();
        await transaction.CommitAsync();
        return result;
    }
}
