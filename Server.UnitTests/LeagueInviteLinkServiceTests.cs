using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// Service-level tests for LeagueInviteLinkService.
/// Uses SQLite in-memory with shared cache because ExecuteUpdateAsync requires real SQL
/// (the EF InMemory provider throws NotImplementedException for bulk update operations).
/// A keepalive connection pins the named in-memory database open for the test duration.
/// </summary>
public class LeagueInviteLinkServiceTests
{
    private static ApplicationDbContext OpenDb(string dbName) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source={dbName};Mode=Memory;Cache=Shared")
            .Options);

    private static IDbContextFactory<ApplicationDbContext> BuildFactory(string dbName)
    {
        var factory = Substitute.For<IDbContextFactory<ApplicationDbContext>>();
        factory.CreateDbContextAsync().Returns(_ => Task.FromResult(OpenDb(dbName)));
        return factory;
    }

    /// <summary>Keeps the named in-memory SQLite database alive for <paramref name="action"/>.</summary>
    private static async Task WithDb(string dbName, Func<IDbContextFactory<ApplicationDbContext>, Task> action)
    {
        // SQLite named in-memory databases are destroyed when the last connection closes.
        // This keepalive connection ensures the database outlives any EF context the test creates.
        await using var keepAlive = new SqliteConnection($"Data Source={dbName};Mode=Memory;Cache=Shared");
        await keepAlive.OpenAsync();
        await using (var init = OpenDb(dbName)) { await init.Database.EnsureCreatedAsync(); }
        await action(BuildFactory(dbName));
    }

    /// <summary>Seed a minimal ApplicationUser row to satisfy the LeagueInviteLink.CreatedByUserId FK.</summary>
    private static async Task SeedUser(ApplicationDbContext db, string userId)
    {
        db.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = userId,
            NormalizedUserName = userId.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
        });
        await db.SaveChangesAsync();
    }

    // ── GenerateAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateAsync_CreatesLink_WithoutReinsertingLeague()
    {
        // Simulates the old bug: controller passes a LeagueInfo fetched from its DbContext
        // into GenerateAsync, which set link.League = <entity from a different context>.
        // EF tracked the entire object graph as Added → Npgsql 23505 PK duplicate in prod.
        var db = nameof(GenerateAsync_CreatesLink_WithoutReinsertingLeague);
        await WithDb(db, async factory =>
        {
            await using (var seed = OpenDb(db))
            {
                await SeedUser(seed, "owner");
                seed.LeagueInfo.Add(new LeagueInfo { Id = 1, LeagueName = "Test", OwnerUserId = "owner" });
                await seed.SaveChangesAsync();
            }

            var service = new LeagueInviteLinkService(factory);
            var link = await service.GenerateAsync(1, "owner");

            Assert.Equal(1, link.LeagueId);
            Assert.NotEmpty(link.Token);
            Assert.True(link.ExpiresAt > DateTimeOffset.UtcNow);

            await using var verify = OpenDb(db);
            Assert.Equal(1, await verify.LeagueInfo.CountAsync());
            Assert.Equal(1, await verify.LeagueInviteLinks.CountAsync());
        });
    }

    [Fact]
    public async Task GenerateAsync_RevokesExistingActiveLink_BeforeCreatingNew()
    {
        var db = nameof(GenerateAsync_RevokesExistingActiveLink_BeforeCreatingNew);
        await WithDb(db, async factory =>
        {
            await using (var seed = OpenDb(db))
            {
                await SeedUser(seed, "owner");
                seed.LeagueInfo.Add(new LeagueInfo { Id = 1, LeagueName = "Test", OwnerUserId = "owner" });
                seed.LeagueInviteLinks.Add(new LeagueInviteLink
                {
                    Token = "old-token", LeagueId = 1, CreatedByUserId = "owner",
                    ExpiresAt = DateTimeOffset.UtcNow.AddHours(12), IsRevoked = false,
                });
                await seed.SaveChangesAsync();
            }

            var service = new LeagueInviteLinkService(factory);
            var newLink = await service.GenerateAsync(1, "owner");

            await using var verify = OpenDb(db);
            Assert.True((await verify.LeagueInviteLinks.SingleAsync(l => l.Token == "old-token")).IsRevoked);
            Assert.NotEqual("old-token", newLink.Token);
            Assert.Equal(2, await verify.LeagueInviteLinks.CountAsync());
        });
    }

    [Fact]
    public async Task GenerateAsync_DoesNotRevokeAlreadyRevokedLinks()
    {
        var db = nameof(GenerateAsync_DoesNotRevokeAlreadyRevokedLinks);
        await WithDb(db, async factory =>
        {
            await using (var seed = OpenDb(db))
            {
                await SeedUser(seed, "owner");
                seed.LeagueInfo.Add(new LeagueInfo { Id = 1, LeagueName = "Test", OwnerUserId = "owner" });
                seed.LeagueInviteLinks.Add(new LeagueInviteLink
                {
                    Token = "already-revoked", LeagueId = 1, CreatedByUserId = "owner",
                    ExpiresAt = DateTimeOffset.UtcNow.AddHours(12), IsRevoked = true,
                });
                await seed.SaveChangesAsync();
            }

            await new LeagueInviteLinkService(factory).GenerateAsync(1, "owner");

            await using var verify = OpenDb(db);
            Assert.Equal(2, await verify.LeagueInviteLinks.CountAsync());
            Assert.True((await verify.LeagueInviteLinks.SingleAsync(l => l.Token == "already-revoked")).IsRevoked);
        });
    }

    // ── GetCurrentAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetCurrentAsync_ReturnsNull_WhenNoLinksExist()
    {
        await WithDb(nameof(GetCurrentAsync_ReturnsNull_WhenNoLinksExist), async factory =>
        {
            var result = await new LeagueInviteLinkService(factory).GetCurrentAsync(99);
            Assert.Null(result);
        });
    }

    [Fact]
    public async Task GetCurrentAsync_ReturnsNull_WhenAllLinksRevoked()
    {
        var db = nameof(GetCurrentAsync_ReturnsNull_WhenAllLinksRevoked);
        await WithDb(db, async factory =>
        {
            await using (var seed = OpenDb(db))
            {
                await SeedUser(seed, "owner");
                seed.LeagueInfo.Add(new LeagueInfo { Id = 1, LeagueName = "Test", OwnerUserId = "owner" });
                seed.LeagueInviteLinks.Add(new LeagueInviteLink
                {
                    Token = "revoked", LeagueId = 1, CreatedByUserId = "owner",
                    ExpiresAt = DateTimeOffset.UtcNow.AddHours(12), IsRevoked = true,
                });
                await seed.SaveChangesAsync();
            }

            var result = await new LeagueInviteLinkService(factory).GetCurrentAsync(1);
            Assert.Null(result);
        });
    }

    [Fact]
    public async Task GetCurrentAsync_ReturnsMostRecentNonRevokedLink()
    {
        var db = nameof(GetCurrentAsync_ReturnsMostRecentNonRevokedLink);
        await WithDb(db, async factory =>
        {
            await using (var seed = OpenDb(db))
            {
                await SeedUser(seed, "owner");
                seed.LeagueInfo.Add(new LeagueInfo { Id = 1, LeagueName = "Test", OwnerUserId = "owner" });
                // Insert "older" first so it gets a lower auto-increment Id.
                // GetCurrentAsync orders by Id descending, so "newer" (higher Id) should win.
                seed.LeagueInviteLinks.AddRange(
                    new LeagueInviteLink { Token = "older", LeagueId = 1, CreatedByUserId = "owner",
                        ExpiresAt = DateTimeOffset.UtcNow.AddHours(22), IsRevoked = false },
                    new LeagueInviteLink { Token = "newer", LeagueId = 1, CreatedByUserId = "owner",
                        ExpiresAt = DateTimeOffset.UtcNow.AddHours(23), IsRevoked = false }
                );
                await seed.SaveChangesAsync();
            }

            var result = await new LeagueInviteLinkService(factory).GetCurrentAsync(1);
            Assert.NotNull(result);
            Assert.Equal("newer", result!.Token);
        });
    }

    [Fact]
    public async Task GetCurrentAsync_ReturnsExpiredLink_IfNotRevoked()
    {
        // Expired-but-not-revoked links are still returned so the UI can show
        // "this link expired — regenerate" rather than a blank invite-link panel.
        var db = nameof(GetCurrentAsync_ReturnsExpiredLink_IfNotRevoked);
        await WithDb(db, async factory =>
        {
            await using (var seed = OpenDb(db))
            {
                await SeedUser(seed, "owner");
                seed.LeagueInfo.Add(new LeagueInfo { Id = 1, LeagueName = "Test", OwnerUserId = "owner" });
                seed.LeagueInviteLinks.Add(new LeagueInviteLink
                {
                    Token = "expired", LeagueId = 1, CreatedByUserId = "owner",
                    ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1), IsRevoked = false,
                });
                await seed.SaveChangesAsync();
            }

            var result = await new LeagueInviteLinkService(factory).GetCurrentAsync(1);
            Assert.NotNull(result);
            Assert.Equal("expired", result!.Token);
        });
    }
}
