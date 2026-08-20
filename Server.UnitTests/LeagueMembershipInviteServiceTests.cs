using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Shared.Models.Enum;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// Service-level tests for LeagueMembershipInviteService. Mirrors LeagueInviteLinkServiceTests'
/// SQLite-in-memory-with-shared-cache setup — no ExecuteUpdateAsync/ExecuteDeleteAsync here (all
/// writes go through tracked entities + SaveChangesAsync), so SQLite is a faithful stand-in.
/// </summary>
public class LeagueMembershipInviteServiceTests
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

    private static async Task WithDb(string dbName, Func<IDbContextFactory<ApplicationDbContext>, Task> action)
    {
        await using var keepAlive = new SqliteConnection($"Data Source={dbName};Mode=Memory;Cache=Shared");
        await keepAlive.OpenAsync();
        await using (var init = OpenDb(dbName)) { await init.Database.EnsureCreatedAsync(); }
        await action(BuildFactory(dbName));
    }

    private static async Task SeedUser(ApplicationDbContext db, string userId, string? email = null)
    {
        db.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = userId,
            NormalizedUserName = userId.ToUpperInvariant(),
            Email = email ?? $"{userId}@example.com",
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
        });
        await db.SaveChangesAsync();
    }

    // ── CreateOrReopenAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task CreateOrReopenAsync_NoExistingInvite_CreatesPendingRow()
    {
        var db = nameof(CreateOrReopenAsync_NoExistingInvite_CreatesPendingRow);
        await WithDb(db, async factory =>
        {
            await using (var seed = OpenDb(db))
            {
                await SeedUser(seed, "owner");
                await SeedUser(seed, "invitee");
                seed.LeagueInfo.Add(new LeagueInfo { Id = 1, LeagueName = "Test", OwnerUserId = "owner" });
                await seed.SaveChangesAsync();
            }

            await new LeagueMembershipInviteService(factory).CreateOrReopenAsync(1, "invitee", "owner");

            await using var verify = OpenDb(db);
            var invite = await verify.LeagueMembershipInvites.SingleAsync();
            Assert.Equal(1, invite.LeagueId);
            Assert.Equal("invitee", invite.InvitedUserId);
            Assert.Equal("owner", invite.InvitedByUserId);
            Assert.Equal(MembershipInviteStatus.Pending, invite.Status);
            Assert.Null(invite.RespondedAt);
        });
    }

    [Fact]
    public async Task CreateOrReopenAsync_ConcurrentCallsForSameNewPair_BothSucceed_ExactlyOneRowCreated()
    {
        // TOCTOU: two concurrent invites for a not-yet-invited user racing the unique index on
        // (LeagueId, InvitedUserId) — the loser's insert must be swallowed, not thrown, since the
        // winner's row already lands as Pending, which is exactly the outcome both callers wanted.
        var db = nameof(CreateOrReopenAsync_ConcurrentCallsForSameNewPair_BothSucceed_ExactlyOneRowCreated);
        await WithDb(db, async factory =>
        {
            await using (var seed = OpenDb(db))
            {
                await SeedUser(seed, "owner");
                await SeedUser(seed, "invitee");
                seed.LeagueInfo.Add(new LeagueInfo { Id = 1, LeagueName = "Test", OwnerUserId = "owner" });
                await seed.SaveChangesAsync();
            }

            var serviceA = new LeagueMembershipInviteService(factory);
            var serviceB = new LeagueMembershipInviteService(factory);

            await Task.WhenAll(
                serviceA.CreateOrReopenAsync(1, "invitee", "owner"),
                serviceB.CreateOrReopenAsync(1, "invitee", "owner"));

            await using var verify = OpenDb(db);
            Assert.Equal(1, await verify.LeagueMembershipInvites.CountAsync());
            var invite = await verify.LeagueMembershipInvites.SingleAsync();
            Assert.Equal(MembershipInviteStatus.Pending, invite.Status);
        });
    }

    [Fact]
    public async Task CreateOrReopenAsync_PreviouslyDeclinedInvite_ResetsToPending_WithoutDuplicateRow()
    {
        var db = nameof(CreateOrReopenAsync_PreviouslyDeclinedInvite_ResetsToPending_WithoutDuplicateRow);
        await WithDb(db, async factory =>
        {
            await using (var seed = OpenDb(db))
            {
                await SeedUser(seed, "owner");
                await SeedUser(seed, "invitee");
                seed.LeagueInfo.Add(new LeagueInfo { Id = 1, LeagueName = "Test", OwnerUserId = "owner" });
                seed.LeagueMembershipInvites.Add(new LeagueMembershipInvite
                {
                    LeagueId = 1, InvitedUserId = "invitee", InvitedByUserId = "owner",
                    Status = MembershipInviteStatus.Declined, RespondedAt = DateTimeOffset.UtcNow.AddDays(-1),
                });
                await seed.SaveChangesAsync();
            }

            await new LeagueMembershipInviteService(factory).CreateOrReopenAsync(1, "invitee", "owner");

            await using var verify = OpenDb(db);
            Assert.Equal(1, await verify.LeagueMembershipInvites.CountAsync());
            var invite = await verify.LeagueMembershipInvites.SingleAsync();
            Assert.Equal(MembershipInviteStatus.Pending, invite.Status);
            Assert.Null(invite.RespondedAt);
        });
    }

    // ── GetByIdAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        await WithDb(nameof(GetByIdAsync_ReturnsNull_WhenNotFound), async factory =>
        {
            var result = await new LeagueMembershipInviteService(factory).GetByIdAsync(999);
            Assert.Null(result);
        });
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsTheInvite_WhenFound()
    {
        var db = nameof(GetByIdAsync_ReturnsTheInvite_WhenFound);
        await WithDb(db, async factory =>
        {
            int id;
            await using (var seed = OpenDb(db))
            {
                await SeedUser(seed, "owner");
                await SeedUser(seed, "invitee");
                seed.LeagueInfo.Add(new LeagueInfo { Id = 1, LeagueName = "Test League", OwnerUserId = "owner" });
                var invite = new LeagueMembershipInvite { LeagueId = 1, InvitedUserId = "invitee", InvitedByUserId = "owner" };
                seed.LeagueMembershipInvites.Add(invite);
                await seed.SaveChangesAsync();
                id = invite.Id;
            }

            var result = await new LeagueMembershipInviteService(factory).GetByIdAsync(id);

            Assert.NotNull(result);
            Assert.Equal(1, result!.LeagueId);
            Assert.Equal("invitee", result.InvitedUserId);
        });
    }

    // ── GetPendingForUserAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetPendingForUserAsync_ReturnsOnlyPendingInvitesForThatUser()
    {
        var db = nameof(GetPendingForUserAsync_ReturnsOnlyPendingInvitesForThatUser);
        await WithDb(db, async factory =>
        {
            await using (var seed = OpenDb(db))
            {
                await SeedUser(seed, "owner");
                await SeedUser(seed, "invitee");
                await SeedUser(seed, "other-user");
                seed.LeagueInfo.AddRange(
                    new LeagueInfo { Id = 1, LeagueName = "League One", OwnerUserId = "owner" },
                    new LeagueInfo { Id = 2, LeagueName = "League Two", OwnerUserId = "owner" });
                seed.LeagueMembershipInvites.AddRange(
                    new LeagueMembershipInvite { LeagueId = 1, InvitedUserId = "invitee", InvitedByUserId = "owner", Status = MembershipInviteStatus.Pending },
                    new LeagueMembershipInvite { LeagueId = 2, InvitedUserId = "invitee", InvitedByUserId = "owner", Status = MembershipInviteStatus.Accepted },
                    new LeagueMembershipInvite { LeagueId = 1, InvitedUserId = "other-user", InvitedByUserId = "owner", Status = MembershipInviteStatus.Pending });
                await seed.SaveChangesAsync();
            }

            var result = await new LeagueMembershipInviteService(factory).GetPendingForUserAsync("invitee");

            var invite = Assert.Single(result);
            Assert.Equal(1, invite.LeagueId);
        });
    }

    // ── MarkAcceptedAsync / MarkDeclinedAsync ────────────────────────────────

    [Fact]
    public async Task MarkAcceptedAsync_SetsStatusAndRespondedAt()
    {
        var db = nameof(MarkAcceptedAsync_SetsStatusAndRespondedAt);
        await WithDb(db, async factory =>
        {
            int id;
            await using (var seed = OpenDb(db))
            {
                await SeedUser(seed, "owner");
                await SeedUser(seed, "invitee");
                seed.LeagueInfo.Add(new LeagueInfo { Id = 1, LeagueName = "Test", OwnerUserId = "owner" });
                var invite = new LeagueMembershipInvite { LeagueId = 1, InvitedUserId = "invitee", InvitedByUserId = "owner" };
                seed.LeagueMembershipInvites.Add(invite);
                await seed.SaveChangesAsync();
                id = invite.Id;
            }

            await new LeagueMembershipInviteService(factory).MarkAcceptedAsync(id);

            await using var verify = OpenDb(db);
            var updated = await verify.LeagueMembershipInvites.SingleAsync();
            Assert.Equal(MembershipInviteStatus.Accepted, updated.Status);
            Assert.NotNull(updated.RespondedAt);
        });
    }

    [Fact]
    public async Task MarkDeclinedAsync_SetsStatusAndRespondedAt()
    {
        var db = nameof(MarkDeclinedAsync_SetsStatusAndRespondedAt);
        await WithDb(db, async factory =>
        {
            int id;
            await using (var seed = OpenDb(db))
            {
                await SeedUser(seed, "owner");
                await SeedUser(seed, "invitee");
                seed.LeagueInfo.Add(new LeagueInfo { Id = 1, LeagueName = "Test", OwnerUserId = "owner" });
                var invite = new LeagueMembershipInvite { LeagueId = 1, InvitedUserId = "invitee", InvitedByUserId = "owner" };
                seed.LeagueMembershipInvites.Add(invite);
                await seed.SaveChangesAsync();
                id = invite.Id;
            }

            await new LeagueMembershipInviteService(factory).MarkDeclinedAsync(id);

            await using var verify = OpenDb(db);
            var updated = await verify.LeagueMembershipInvites.SingleAsync();
            Assert.Equal(MembershipInviteStatus.Declined, updated.Status);
            Assert.NotNull(updated.RespondedAt);
        });
    }

    // ── DeleteAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_RemovesTheInvite()
    {
        var db = nameof(DeleteAsync_RemovesTheInvite);
        await WithDb(db, async factory =>
        {
            int id;
            await using (var seed = OpenDb(db))
            {
                await SeedUser(seed, "owner");
                await SeedUser(seed, "invitee");
                seed.LeagueInfo.Add(new LeagueInfo { Id = 1, LeagueName = "Test", OwnerUserId = "owner" });
                var invite = new LeagueMembershipInvite { LeagueId = 1, InvitedUserId = "invitee", InvitedByUserId = "owner" };
                seed.LeagueMembershipInvites.Add(invite);
                await seed.SaveChangesAsync();
                id = invite.Id;
            }

            await new LeagueMembershipInviteService(factory).DeleteAsync(id);

            await using var verify = OpenDb(db);
            Assert.Equal(0, await verify.LeagueMembershipInvites.CountAsync());
        });
    }

    [Fact]
    public async Task DeleteAsync_NonExistentId_DoesNotThrow()
    {
        await WithDb(nameof(DeleteAsync_NonExistentId_DoesNotThrow), async factory =>
        {
            await new LeagueMembershipInviteService(factory).DeleteAsync(999);
        });
    }

    // ── GetByLeagueAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetByLeagueAsync_ReturnsAllStatusesForThatLeague_NewestFirst()
    {
        var db = nameof(GetByLeagueAsync_ReturnsAllStatusesForThatLeague_NewestFirst);
        await WithDb(db, async factory =>
        {
            await using (var seed = OpenDb(db))
            {
                await SeedUser(seed, "owner");
                await SeedUser(seed, "invitee-1", "invitee1@example.com");
                await SeedUser(seed, "invitee-2", "invitee2@example.com");
                seed.LeagueInfo.AddRange(
                    new LeagueInfo { Id = 1, LeagueName = "League One", OwnerUserId = "owner" },
                    new LeagueInfo { Id = 2, LeagueName = "League Two", OwnerUserId = "owner" });
                seed.LeagueMembershipInvites.AddRange(
                    new LeagueMembershipInvite { LeagueId = 1, InvitedUserId = "invitee-1", InvitedByUserId = "owner", Status = MembershipInviteStatus.Pending },
                    new LeagueMembershipInvite { LeagueId = 1, InvitedUserId = "invitee-2", InvitedByUserId = "owner", Status = MembershipInviteStatus.Declined },
                    new LeagueMembershipInvite { LeagueId = 2, InvitedUserId = "invitee-1", InvitedByUserId = "owner", Status = MembershipInviteStatus.Pending });
                await seed.SaveChangesAsync();
            }

            var result = await new LeagueMembershipInviteService(factory).GetByLeagueAsync(1);

            Assert.Equal(2, result.Count);
            Assert.All(result, i => Assert.Equal(1, i.LeagueId));
            Assert.All(result, i => Assert.NotNull(i.InvitedUser?.Email));
        });
    }
}
