using FourPlayWebApp.Shared.Models.Enum;
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
    private static NflSeasonWeekConfig Config(int season, int weekId, DateTime start, DateTime end,
        bool isPostSeason = false, int requiredPicks = 4) => new() {
        Season = season,
        WeekId = weekId,
        IsPostSeason = isPostSeason,
        RequiredPicks = requiredPicks,
        WeekLabel = $"Week {weekId}",
        WeekType = WeekType.RegularSeason,
        ScoringFormat = ScoringFormat.Standard,
        WeekStartDatetime = start,
        WeekEndDatetime = end,
        SpreadLockDatetime = start,
    };

    private static NflCurrentWeekService BuildService(List<NflSeasonWeekConfig> configs, TimeProvider? timeProvider = null) {
        var repo = Substitute.For<ILeagueRepository>();
        repo.GetNflSeasonWeekConfigsAsync().Returns(configs);
        return new NflCurrentWeekService(repo, timeProvider ?? TimeProvider.System);
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

    // frizat-9xg: a week's own SpreadLockDatetime — not its calendar WeekStartDatetime — is
    // what the resolver actually keys off. This distinguishes the two so the test can't pass
    // by accident the way the other cases here do (their fixtures set SpreadLockDatetime ==
    // WeekStartDatetime).
    [Fact]
    public async Task GetCurrentWeekAsync_StaysOnPreviousWeek_WhenNextWeeksSpreadLockIsMoreThanOneDayAway() {
        var now = DateTime.UtcNow;
        var week4 = Config(2026, 4, now.AddDays(-10), now.AddDays(-3));
        week4.SpreadLockDatetime = now.AddDays(-9); // already started
        var week5 = Config(2026, 5, now.AddDays(-3), now.AddDays(4)); // calendar-active right now
        week5.SpreadLockDatetime = now.AddDays(5); // but its own spread grab is 5 days out

        var result = await BuildService([week4, week5]).GetCurrentWeekAsync();

        Assert.Equal(4, result.WeekId);
    }

    [Fact]
    public async Task GetCurrentWeekAsync_EarlyActivatesNextWeek_WhenWithinOneDayOfItsSpreadLock() {
        var now = DateTime.UtcNow;
        var week4 = Config(2026, 4, now.AddDays(-10), now.AddDays(-3));
        week4.SpreadLockDatetime = now.AddDays(-9);
        var week5 = Config(2026, 5, now.AddDays(3), now.AddDays(10)); // hasn't calendar-started yet
        week5.SpreadLockDatetime = now.AddHours(12); // but its spread grab is only 12 hours out

        var result = await BuildService([week4, week5]).GetCurrentWeekAsync();

        Assert.Equal(5, result.WeekId);
    }

    // A league started now begins with the first week of its season that isn't over yet — not the
    // "current" week, which can still be last week for a day after its Monday-night game.
    [Fact]
    public async Task GetFirstUnfinishedWeekAsync_SkipsAWeekThatsOver_EvenWhileItsStillCurrent() {
        var now = DateTime.UtcNow;
        var week4 = Config(2026, 4, now.AddDays(-7), now.AddHours(-10)); // ended last night
        week4.SpreadLockDatetime = now.AddDays(-5);
        var week5 = Config(2026, 5, now.AddHours(-10), now.AddDays(6));
        week5.SpreadLockDatetime = now.AddDays(2); // still "current": week 4
        var svc = BuildService([week5, week4, Config(2025, 22, now.AddDays(-200), now.AddDays(-190))]);

        Assert.Equal(4, (await svc.GetCurrentWeekAsync()).WeekId);
        Assert.Equal(5, (await svc.GetFirstUnfinishedWeekAsync(2026))!.WeekId);
        Assert.Null(await svc.GetFirstUnfinishedWeekAsync(2025));
    }

    // frizat-3nv: NflWeekInfo no longer carries an EspnWeek field at all — nothing on the
    // backend or frontend needs ESPN's own week numbering anymore, only WeekId. ToWeekInfo's
    // former WeekId->EspnWeek mapping (and its unconditional, non-season-gated Pro-Bowl-skip
    // special case, flagged separately under frizat-4k9) is gone, not relocated.
    [Fact]
    public async Task GetCurrentWeekAsync_ReturnsTheRowsPostSeasonFlagAndPickCount() {
        // frizat-d47: straight from the control-table row, never derived from the WeekId.
        var now = DateTime.UtcNow;
        var svc = BuildService([
            Config(2025, 22, now.AddDays(-1), now.AddDays(1), isPostSeason: true, requiredPicks: 1),
        ]);

        var result = await svc.GetCurrentWeekAsync();

        Assert.Equal(22, result.WeekId);
        Assert.True(result.IsPostSeason);
        Assert.Equal(1, result.RequiredPicks);
    }

    // frizat-tf2: GetCurrentWeekAsync must resolve "current" against an injected TimeProvider
    // (used to freeze the clock in DEMO_MODE — see DemoDataSeeder.DemoFrozenNow), not the real
    // wall clock. Two windows are constructed so a real-clock read (long past both locks by now)
    // would resolve to week 22, while the frozen clock correctly stays on week 21 — a bug that
    // silently reverted to DateTime.UtcNow would still pass every other test in this file (they
    // all default to TimeProvider.System, which behaves identically to DateTime.UtcNow).
    [Fact]
    public async Task GetCurrentWeekAsync_UsesInjectedClock_NotRealWallClock() {
        var frozen = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var week21 = Config(2025, 21, frozen.UtcDateTime.AddDays(-10), frozen.UtcDateTime.AddDays(-3));
        var week22 = Config(2025, 22, frozen.UtcDateTime.AddDays(10), frozen.UtcDateTime.AddDays(17));
        var svc = BuildService([week21, week22], new FakeTimeProvider(frozen));

        var result = await svc.GetCurrentWeekAsync();

        Assert.Equal(21, result.WeekId);
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
