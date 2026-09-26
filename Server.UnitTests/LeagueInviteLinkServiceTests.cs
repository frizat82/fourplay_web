using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// Service-level tests for LeagueInviteLinkService on in-memory SQLite (the EF InMemory provider
/// can't run the service's ExecuteUpdateAsync at all). Which links a Generate/Revoke actually
/// revokes is a bulk update, so that's proven on real Postgres instead, per CLAUDE.md — see
/// LeagueInviteLinkServicePostgresTests.
/// </summary>
public class LeagueInviteLinkServiceTests
{
    // ── GenerateAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateAsync_CreatesLink_WithoutReinsertingLeague()
    {
        // Simulates the old bug: controller passes a LeagueInfo fetched from its DbContext
        // into GenerateAsync, which set link.League = <entity from a different context>.
        // EF tracked the entire object graph as Added → Npgsql 23505 PK duplicate in prod.
        var db = nameof(GenerateAsync_CreatesLink_WithoutReinsertingLeague);
        await SqliteTestDb.WithDb(db, async factory =>
        {
            await using (var seed = SqliteTestDb.Open(db))
            {
                await SqliteTestDb.SeedUser(seed, "owner");
                seed.LeagueInfo.Add(new LeagueInfo { Id = 1, LeagueName = "Test", OwnerUserId = "owner" });
                await seed.SaveChangesAsync();
            }

            var service = new LeagueInviteLinkService(factory);
            var link = await service.GenerateAsync(1, "owner");

            Assert.Equal(1, link.LeagueId);
            Assert.NotEmpty(link.Token);
            Assert.True(link.ExpiresAt > DateTimeOffset.UtcNow);

            await using var verify = SqliteTestDb.Open(db);
            Assert.Equal(1, await verify.LeagueInfo.CountAsync());
            Assert.Equal(1, await verify.LeagueInviteLinks.CountAsync());
        });
    }

    [Fact]
    public async Task GenerateAsync_DoesNotRevokeAlreadyRevokedLinks()
    {
        var db = nameof(GenerateAsync_DoesNotRevokeAlreadyRevokedLinks);
        await SqliteTestDb.WithDb(db, async factory =>
        {
            await using (var seed = SqliteTestDb.Open(db))
            {
                await SqliteTestDb.SeedUser(seed, "owner");
                seed.LeagueInfo.Add(new LeagueInfo { Id = 1, LeagueName = "Test", OwnerUserId = "owner" });
                seed.LeagueInviteLinks.Add(new LeagueInviteLink
                {
                    Token = "already-revoked", LeagueId = 1, CreatedByUserId = "owner",
                    ExpiresAt = DateTimeOffset.UtcNow.AddHours(12), IsRevoked = true,
                });
                await seed.SaveChangesAsync();
            }

            await new LeagueInviteLinkService(factory).GenerateAsync(1, "owner");

            await using var verify = SqliteTestDb.Open(db);
            Assert.Equal(2, await verify.LeagueInviteLinks.CountAsync());
            Assert.True((await verify.LeagueInviteLinks.SingleAsync(l => l.Token == "already-revoked")).IsRevoked);
        });
    }

    // ── GetCurrentAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetCurrentAsync_ReturnsNull_WhenNoLinksExist()
    {
        await SqliteTestDb.WithDb(nameof(GetCurrentAsync_ReturnsNull_WhenNoLinksExist), async factory =>
        {
            var result = await new LeagueInviteLinkService(factory).GetCurrentAsync(99);
            Assert.Null(result);
        });
    }

    [Fact]
    public async Task GetCurrentAsync_ReturnsNull_WhenAllLinksRevoked()
    {
        var db = nameof(GetCurrentAsync_ReturnsNull_WhenAllLinksRevoked);
        await SqliteTestDb.WithDb(db, async factory =>
        {
            await using (var seed = SqliteTestDb.Open(db))
            {
                await SqliteTestDb.SeedUser(seed, "owner");
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
        await SqliteTestDb.WithDb(db, async factory =>
        {
            await using (var seed = SqliteTestDb.Open(db))
            {
                await SqliteTestDb.SeedUser(seed, "owner");
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
        await SqliteTestDb.WithDb(db, async factory =>
        {
            await using (var seed = SqliteTestDb.Open(db))
            {
                await SqliteTestDb.SeedUser(seed, "owner");
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

    // ── RevokeAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RevokeAsync_NoActiveLink_DoesNotThrow()
    {
        await SqliteTestDb.WithDb(nameof(RevokeAsync_NoActiveLink_DoesNotThrow), async factory =>
        {
            await new LeagueInviteLinkService(factory).RevokeAsync(999);
        });
    }
}
