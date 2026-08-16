using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// TDD tests: DemoDataSeeder must purge leagues it didn't itself create (e.g. self-serve/admin
/// "Create League" test data left over from manual testing) on every startup, so the local demo
/// stack stays genuinely idempotent instead of accumulating stray leagues forever.
/// </summary>
public class DemoDataSeederLeaguePurgeTests {
    private static ApplicationDbContext BuildDb(string name) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name)
            .Options);

    private static UserManager<ApplicationUser> BuildUserManager() {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        return Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);
    }

    private static IConfiguration BuildConfiguration() =>
        new ConfigurationBuilder().Build();

    [Fact]
    public async Task PurgeUnknownLeaguesAsync_removes_leagues_the_seeder_did_not_create() {
        await using var db = BuildDb(nameof(PurgeUnknownLeaguesAsync_removes_leagues_the_seeder_did_not_create));

        var demoLeague = new LeagueInfo { LeagueName = "Demo League", OwnerUserId = "admin-1" };
        var cfbDemoLeague = new LeagueInfo { LeagueName = "CFB Demo League", OwnerUserId = "admin-1" };
        var strayLeague = new LeagueInfo { LeagueName = "Alice's Test League", OwnerUserId = "alice-1" };
        db.LeagueInfo.AddRange(demoLeague, cfbDemoLeague, strayLeague);
        await db.SaveChangesAsync();

        db.LeagueUserMapping.Add(new LeagueUserMapping { LeagueId = strayLeague.Id, UserId = "alice-1" });
        db.LeagueJuiceMapping.Add(new LeagueJuiceMapping {
            LeagueId = strayLeague.Id, Season = 2025, Juice = 13, JuiceDivisional = 10,
            JuiceConference = 6, WeeklyCost = 5, DateCreated = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var seeder = new DemoDataSeeder(db, BuildUserManager(), BuildConfiguration());
        await seeder.PurgeUnknownLeaguesAsync();

        Assert.False(await db.LeagueInfo.AnyAsync(l => l.Id == strayLeague.Id));
        Assert.False(await db.LeagueUserMapping.AnyAsync(m => m.LeagueId == strayLeague.Id));
        Assert.False(await db.LeagueJuiceMapping.AnyAsync(m => m.LeagueId == strayLeague.Id));
        Assert.True(await db.LeagueInfo.AnyAsync(l => l.Id == demoLeague.Id));
        Assert.True(await db.LeagueInfo.AnyAsync(l => l.Id == cfbDemoLeague.Id));
    }

    [Fact]
    public async Task PurgeUnknownLeaguesAsync_is_a_no_op_when_only_canonical_leagues_exist() {
        await using var db = BuildDb(nameof(PurgeUnknownLeaguesAsync_is_a_no_op_when_only_canonical_leagues_exist));

        db.LeagueInfo.AddRange(
            new LeagueInfo { LeagueName = "Demo League", OwnerUserId = "admin-1" },
            new LeagueInfo { LeagueName = "CFB Demo League", OwnerUserId = "admin-1" });
        await db.SaveChangesAsync();

        var seeder = new DemoDataSeeder(db, BuildUserManager(), BuildConfiguration());
        await seeder.PurgeUnknownLeaguesAsync();

        Assert.Equal(2, await db.LeagueInfo.CountAsync());
    }

    // frizat: DemoDataSeeder's class doc comment promises "Idempotent — safe to call on every
    // startup." Before soft-delete, a removed member's row was hard-deleted, so a bare
    // AnyAsync-then-Add self-healed on reseed. Now the row persists with IsActive=false, so a
    // naive AnyAsync check sees it as "already exists" and never reactivates it — permanently
    // excluding a demo user from the league the moment anyone exercises the new Remove Member
    // feature against the demo stack, contradicting the seeder's own idempotency contract.
    [Fact]
    public async Task EnsureActiveLeagueMemberAsync_ReactivatesExistingInactiveRow() {
        await using var db = BuildDb(nameof(EnsureActiveLeagueMemberAsync_ReactivatesExistingInactiveRow));
        var league = new LeagueInfo { LeagueName = "Demo League", OwnerUserId = "admin-1" };
        db.LeagueInfo.Add(league);
        await db.SaveChangesAsync();
        var originalJoinDate = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        db.LeagueUserMapping.Add(new LeagueUserMapping {
            LeagueId = league.Id, UserId = "eve-1", IsActive = false,
            RemovedAt = DateTimeOffset.UtcNow, DateCreated = originalJoinDate,
        });
        await db.SaveChangesAsync();

        var seeder = new DemoDataSeeder(db, BuildUserManager(), BuildConfiguration());
        await seeder.EnsureActiveLeagueMemberAsync(league.Id, "eve-1");

        var mappings = await db.LeagueUserMapping.Where(m => m.LeagueId == league.Id && m.UserId == "eve-1").ToListAsync();
        Assert.Single(mappings);
        Assert.True(mappings[0].IsActive);
        Assert.Null(mappings[0].RemovedAt);
        Assert.Equal(originalJoinDate, mappings[0].DateCreated);
    }

    [Fact]
    public async Task EnsureActiveLeagueMemberAsync_InsertsNewRow_WhenNoneExists() {
        await using var db = BuildDb(nameof(EnsureActiveLeagueMemberAsync_InsertsNewRow_WhenNoneExists));
        var league = new LeagueInfo { LeagueName = "Demo League", OwnerUserId = "admin-1" };
        db.LeagueInfo.Add(league);
        await db.SaveChangesAsync();

        var seeder = new DemoDataSeeder(db, BuildUserManager(), BuildConfiguration());
        await seeder.EnsureActiveLeagueMemberAsync(league.Id, "eve-1");

        var mapping = await db.LeagueUserMapping.SingleAsync(m => m.LeagueId == league.Id && m.UserId == "eve-1");
        Assert.True(mapping.IsActive);
    }

    [Fact]
    public async Task EnsureActiveLeagueMemberAsync_IsANoOp_WhenAlreadyActive() {
        await using var db = BuildDb(nameof(EnsureActiveLeagueMemberAsync_IsANoOp_WhenAlreadyActive));
        var league = new LeagueInfo { LeagueName = "Demo League", OwnerUserId = "admin-1" };
        db.LeagueInfo.Add(league);
        await db.SaveChangesAsync();
        db.LeagueUserMapping.Add(new LeagueUserMapping { LeagueId = league.Id, UserId = "eve-1" });
        await db.SaveChangesAsync();

        var seeder = new DemoDataSeeder(db, BuildUserManager(), BuildConfiguration());
        await seeder.EnsureActiveLeagueMemberAsync(league.Id, "eve-1");

        Assert.Single(await db.LeagueUserMapping.Where(m => m.LeagueId == league.Id && m.UserId == "eve-1").ToListAsync());
    }
}
