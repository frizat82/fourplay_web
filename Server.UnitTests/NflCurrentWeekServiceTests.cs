using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using NSubstitute;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

// Direct unit tests for NflCurrentWeekService's resolution logic — previously only
// exercised indirectly through NflCurrentWeekEndpointTests (which mocks the service
// itself, so never actually ran this fallback chain). Added while refactoring the service
// to call the shared SeasonWindowResolver instead of its own hand-rolled ??= chain
// (frizat plan: wobbly-chasing-lynx) — these lock in the documented off-season/pre-season
// fallback behavior as a regression guard for that refactor.
public class NflCurrentWeekServiceTests
{
    private static NflSeasonWeekConfig Config(int season, int weekId, DateTime start, DateTime end) => new() {
        Season = season,
        WeekId = weekId,
        WeekLabel = $"Week {weekId}",
        WeekType = "Regular",
        ScoringFormat = "Standard",
        WeekStartDatetime = start,
        WeekEndDatetime = end,
        SpreadLockDatetime = start,
    };

    private static NflCurrentWeekService BuildService(List<NflSeasonWeekConfig> configs) {
        var repo = Substitute.For<ILeagueRepository>();
        repo.GetNflSeasonWeekConfigsAsync().Returns(configs);
        return new NflCurrentWeekService(repo);
    }

    [Fact]
    public async Task GetCurrentWeekAsync_ReturnsActiveWeek_WhenNowIsWithinOne() {
        var now = DateTime.UtcNow;
        var svc = BuildService([
            Config(2026, 4, now.AddDays(-3), now.AddDays(4)),
            Config(2026, 5, now.AddDays(4), now.AddDays(11)),
        ]);

        var result = await svc.GetCurrentWeekAsync();

        Assert.Equal(4, result.WeekId);
        Assert.Equal(2026, result.Season);
    }

    [Fact]
    public async Task GetCurrentWeekAsync_ReturnsMostRecentlyCompleted_OffSeason() {
        var now = DateTime.UtcNow;
        var svc = BuildService([
            Config(2025, 21, now.AddDays(-60), now.AddDays(-53)),
            Config(2025, 22, now.AddDays(-30), now.AddDays(-23)), // most recently completed
        ]);

        var result = await svc.GetCurrentWeekAsync();

        Assert.Equal(22, result.WeekId);
        Assert.Equal(2025, result.Season);
    }

    [Fact]
    public async Task GetCurrentWeekAsync_ReturnsSoonestUpcoming_PreSeason() {
        var now = DateTime.UtcNow;
        var svc = BuildService([
            Config(2026, 1, now.AddDays(10), now.AddDays(17)), // soonest upcoming
            Config(2026, 2, now.AddDays(17), now.AddDays(24)),
        ]);

        var result = await svc.GetCurrentWeekAsync();

        Assert.Equal(1, result.WeekId);
    }

    [Fact]
    public async Task GetCurrentWeekAsync_MapsEspnWeek_ForPostseasonRounds() {
        // ToWeekInfo's WeekId -> ESPN week mapping is unchanged by this refactor — Super
        // Bowl (WeekId 22) maps to ESPN week 5 (ESPN skips week 4 = Pro Bowl).
        var now = DateTime.UtcNow;
        var svc = BuildService([
            Config(2025, 22, now.AddDays(-1), now.AddDays(1)),
        ]);

        var result = await svc.GetCurrentWeekAsync();

        Assert.Equal(22, result.WeekId);
        Assert.Equal(5, result.EspnWeek);
        Assert.True(result.IsPostSeason);
    }

    [Fact]
    public async Task IsSeasonActiveAsync_True_WhenNowIsWithinAWindow() {
        var now = DateTime.UtcNow;
        var svc = BuildService([Config(2026, 4, now.AddDays(-3), now.AddDays(4))]);

        Assert.True(await svc.IsSeasonActiveAsync());
    }

    [Fact]
    public async Task IsSeasonActiveAsync_False_OffSeason() {
        var now = DateTime.UtcNow;
        var svc = BuildService([Config(2025, 22, now.AddDays(-60), now.AddDays(-53))]);

        Assert.False(await svc.IsSeasonActiveAsync());
    }
}
