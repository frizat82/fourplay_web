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

// frizat-tf2: SeedReplayGameSpreadAsync used to self-heal its NflSeasonWeekConfigs row relative to
// the real wall clock so SeasonWindowResolver would always resolve it as "current" — but that
// broke the instant real wall-clock came within 2 days of a REAL, unseeded future season's own
// SpreadLockDatetime (SeasonWindowResolver's early-activation rule doesn't care how recently the
// replay row's own lock passed — confirmed by replay-nfl.spec.ts's "Pick IND" button failing in CI
// once real time crossed that threshold). This regression test locks in the fix: the row's window
// fields (used only for "what's current" resolution) are anchored to DemoDataSeeder.DemoFrozenNow
// instead, while NflSpreads.GameTime (compared against the real browser clock by PicksPage.tsx for
// pick eligibility) stays anchored to the real wall clock.
public class DemoDataSeederReplayClockTests {
    private const int ReplaySeason = 2026;
    private const int ReplayWeek = 22;

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
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["DEMO_REPLAY_MODE"] = "true" })
            .Build();

    [Fact]
    public async Task SeedReplayGameSpreadAsync_AnchorsWeekConfigWindow_ToDemoFrozenNow_NotRealClock() {
        await using var db = BuildDb(nameof(SeedReplayGameSpreadAsync_AnchorsWeekConfigWindow_ToDemoFrozenNow_NotRealClock));
        // Pre-existing row with real, far-future calendar dates — proves the fix overwrites them
        // with DemoFrozenNow-anchored values rather than leaving/self-healing them to real "now".
        db.NflSeasonWeekConfigs.Add(new NflSeasonWeekConfig {
            Season = ReplaySeason, WeekId = ReplayWeek, WeekLabel = "Super Bowl", WeekType = "PostSeason",
            ScoringFormat = "Standard",
            WeekStartDatetime = new DateTime(2027, 2, 10), WeekEndDatetime = new DateTime(2027, 2, 17),
            SpreadLockDatetime = new DateTime(2027, 2, 14),
        });
        await db.SaveChangesAsync();

        var seeder = new DemoDataSeeder(db, BuildUserManager(), BuildConfiguration());
        await seeder.SeedReplayGameSpreadAsync();

        var config = await db.NflSeasonWeekConfigs.SingleAsync(c => c.Season == ReplaySeason && c.WeekId == ReplayWeek);
        Assert.Equal(DemoDataSeeder.DemoFrozenNow.AddHours(-2).UtcDateTime, config.WeekStartDatetime);
        Assert.Equal(DemoDataSeeder.DemoFrozenNow.AddDays(1).UtcDateTime, config.WeekEndDatetime);
        Assert.Equal(DemoDataSeeder.DemoFrozenNow.AddHours(-1).UtcDateTime, config.SpreadLockDatetime);
    }

    [Fact]
    public async Task SeedReplayGameSpreadAsync_KeepsSpreadGameTime_OnTheRealClock() {
        await using var db = BuildDb(nameof(SeedReplayGameSpreadAsync_KeepsSpreadGameTime_OnTheRealClock));
        var before = DateTimeOffset.UtcNow;

        var seeder = new DemoDataSeeder(db, BuildUserManager(), BuildConfiguration());
        await seeder.SeedReplayGameSpreadAsync();

        var spread = await db.NflSpreads.SingleAsync(s =>
            s.Season == ReplaySeason && s.NflWeek == ReplayWeek && s.HomeTeam == "IND" && s.AwayTeam == "ATL");
        // PicksPage.tsx compares GameTime against the browser's real wall clock (new Date()), not
        // any backend TimeProvider — it must stay real-time-anchored (real now + ~2h) so the game
        // reads as "not started yet" for the pick button. DemoFrozenNow is a fixed February 2026
        // point, long in the past by the time this test actually runs, so this also rules out a
        // regression that anchors GameTime to DemoFrozenNow like the window fields above.
        Assert.True(spread.GameTime > before.AddHours(1));
        Assert.True(spread.GameTime < before.AddHours(3));
    }
}
