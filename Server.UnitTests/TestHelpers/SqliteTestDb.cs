using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Models.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// A named, shared-cache in-memory SQLite database — for tests that need a relational provider
/// (constraints, FKs, transactions) but not Postgres-specific behaviour.
/// </summary>
internal static class SqliteTestDb {
    public static ApplicationDbContext Open(string dbName) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source={dbName};Mode=Memory;Cache=Shared")
            .Options);

    public static IDbContextFactory<ApplicationDbContext> Factory(string dbName) {
        var factory = Substitute.For<IDbContextFactory<ApplicationDbContext>>();
        factory.CreateDbContextAsync().Returns(_ => Task.FromResult(Open(dbName)));
        return factory;
    }

    /// <summary>
    /// Creates the schema and keeps the database alive for <paramref name="action"/> — a named
    /// in-memory SQLite database is destroyed when its last connection closes.
    /// </summary>
    public static async Task WithDb(string dbName, Func<IDbContextFactory<ApplicationDbContext>, Task> action) {
        await using var keepAlive = new SqliteConnection($"Data Source={dbName};Mode=Memory;Cache=Shared");
        await keepAlive.OpenAsync();
        await using (var init = Open(dbName)) { await init.Database.EnsureCreatedAsync(); }
        await action(Factory(dbName));
    }

    /// <summary>A minimal ApplicationUser row, enough to satisfy user FKs.</summary>
    public static async Task SeedUser(ApplicationDbContext db, string userId, string? email = null) {
        db.Users.Add(new ApplicationUser {
            Id = userId,
            UserName = userId,
            NormalizedUserName = userId.ToUpperInvariant(),
            Email = email ?? $"{userId}@example.com",
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
        });
        await db.SaveChangesAsync();
    }
}
