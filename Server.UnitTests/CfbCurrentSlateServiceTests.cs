using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using NSubstitute;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

// Direct unit tests for CfbCurrentSlateService — written while replacing its hardcoded
// `ConfiguredSeason = 2026` constant + manual "season - 1" fallback with the shared
// SeasonWindowResolver (frizat plan: wobbly-chasing-lynx). The season-2030 case below is
// the actual regression test for the bug: the old hardcoded-year implementation could
// never resolve any season other than 2026/2025 without a code change.
public class CfbCurrentSlateServiceTests
{
    private static CfbSeasonWeekConfig Slate(int season, int slateNumber, DateOnly start, DateOnly end, DateTime? spreadLock = null) => new() {
        Id = season * 100 + slateNumber,
        Season = season,
        IvLeagueWeekNumber = slateNumber, InScopeIvLeague = true,
        WeekLabel = $"Slate {slateNumber}",
        WeekStartDate = start,
        WeekEndDate = end,
        SpreadLockDatetime = spreadLock ?? DateTime.UtcNow,
    };

    private static CfbCurrentSlateService BuildService(List<CfbSeasonWeekConfig> slates, TimeProvider? timeProvider = null) {
        var repo = Substitute.For<ICfbRepository>();
        repo.GetAllSlatesAsync().Returns(slates);
        return new CfbCurrentSlateService(repo, timeProvider ?? TimeProvider.System);
    }

    [Fact]
    public async Task GetCurrentSlateAsync_ResolvesFromAnySeasonInTheDb_NoHardcodedYear() {
        // Regression test for the hardcoded-ConfiguredSeason bug: season 2030 has no
        // special-casing anywhere in the service — it must resolve purely from what's in
        // the DB.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var svc = BuildService([
            Slate(2030, 1, today.AddDays(-3), today.AddDays(4)),
        ]);

        var result = await svc.GetCurrentSlateAsync();

        Assert.NotNull(result);
        Assert.Equal(2030, result.Season);
        Assert.Equal(1, result.SlateNumber);
    }

    [Fact]
    public async Task GetCurrentSlateAsync_ReturnsActiveSlate_WhenTodayIsWithinOne() {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var svc = BuildService([
            Slate(2026, 1, today.AddDays(-10), today.AddDays(-4)),
            Slate(2026, 2, today.AddDays(-3), today.AddDays(4)), // active
        ]);

        var result = await svc.GetCurrentSlateAsync();

        Assert.Equal(2, result!.SlateNumber);
    }

    [Fact]
    public async Task GetCurrentSlateAsync_ReturnsMostRecentlyCompleted_OffSeason() {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var svc = BuildService([
            Slate(2025, 3, today.AddDays(-60), today.AddDays(-53)),
            Slate(2025, 4, today.AddDays(-30), today.AddDays(-23)), // most recently completed
        ]);

        var result = await svc.GetCurrentSlateAsync();

        Assert.Equal(2025, result!.Season);
        Assert.Equal(4, result.SlateNumber);
    }

    // frizat-9xg: a slate's own SpreadLockDatetime — not its calendar StartDate/EndDate — is what
    // the resolver actually keys off.
    [Fact]
    public async Task GetCurrentSlateAsync_StaysOnPreviousSlate_WhenNextSlatesSpreadLockIsMoreThanTwoDaysAway() {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTime.UtcNow;
        var slate3 = Slate(2026, 3, today.AddDays(-10), today.AddDays(-3), now.AddDays(-9));
        var slate4 = Slate(2026, 4, today.AddDays(-3), today.AddDays(4), now.AddDays(5)); // calendar-active right now, lock 5 days out
        var repo = Substitute.For<ICfbRepository>();
        repo.GetAllSlatesAsync().Returns(new List<CfbSeasonWeekConfig> { slate3, slate4 });

        var result = await new CfbCurrentSlateService(repo, TimeProvider.System).GetCurrentSlateAsync();

        Assert.Equal(3, result!.SlateNumber);
    }

    [Fact]
    public async Task GetCurrentSlateAsync_EarlyActivatesNextSlate_WhenWithinTwoDaysOfItsSpreadLock() {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTime.UtcNow;
        var slate3 = Slate(2026, 3, today.AddDays(-10), today.AddDays(-3), now.AddDays(-9));
        var slate4 = Slate(2026, 4, today.AddDays(3), today.AddDays(10), now.AddDays(1)); // hasn't calendar-started yet, lock 1 day out
        var repo = Substitute.For<ICfbRepository>();
        repo.GetAllSlatesAsync().Returns(new List<CfbSeasonWeekConfig> { slate3, slate4 });

        var result = await new CfbCurrentSlateService(repo, TimeProvider.System).GetCurrentSlateAsync();

        Assert.Equal(4, result!.SlateNumber);
    }

    [Fact]
    public async Task GetCurrentSlateAsync_ReturnsNull_WhenNoSlatesExistAtAll() {
        var svc = BuildService([]);

        var result = await svc.GetCurrentSlateAsync();

        Assert.Null(result);
    }

    // frizat-tf2: GetCurrentSlateAsync must resolve "current" against an injected TimeProvider
    // (used to freeze the clock in DEMO_MODE — see DemoDataSeeder.DemoFrozenNow), not the real
    // wall clock. Two slates are constructed so a real-clock read (long past both locks by now)
    // would resolve to slate 18, while the frozen clock correctly stays on slate 17.
    [Fact]
    public async Task GetCurrentSlateAsync_UsesInjectedClock_NotRealWallClock() {
        var frozen = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var today = DateOnly.FromDateTime(frozen.UtcDateTime);
        var slate17 = Slate(2025, 17, today.AddDays(-10), today.AddDays(-3), frozen.UtcDateTime.AddDays(-10));
        var slate18 = Slate(2025, 18, today.AddDays(10), today.AddDays(17), frozen.UtcDateTime.AddDays(10));
        var repo = Substitute.For<ICfbRepository>();
        repo.GetAllSlatesAsync().Returns(new List<CfbSeasonWeekConfig> { slate17, slate18 });

        var result = await new CfbCurrentSlateService(repo, new FakeTimeProvider(frozen)).GetCurrentSlateAsync();

        Assert.Equal(17, result!.SlateNumber);
    }

    [Fact]
    public async Task IsSeasonActiveAsync_True_WhenTodayIsWithinASlateWindow() {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var svc = BuildService([Slate(2026, 1, today.AddDays(-3), today.AddDays(4))]);

        Assert.True(await svc.IsSeasonActiveAsync());
    }

    [Fact]
    public async Task IsSeasonActiveAsync_False_OffSeason() {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var svc = BuildService([Slate(2025, 4, today.AddDays(-60), today.AddDays(-53))]);

        Assert.False(await svc.IsSeasonActiveAsync());
    }
}
