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
}
