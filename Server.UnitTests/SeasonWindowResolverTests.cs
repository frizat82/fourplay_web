using FourPlayWebApp.Server.Services;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

// SeasonWindowResolver is the shared, pure NFL/CFB season-resolution logic (frizat plan:
// wobbly-chasing-lynx) — replaces two independent hand-rolled implementations
// (NflCurrentWeekService's ??= fallback chain, CfbCurrentSlateService's hardcoded
// ConfiguredSeason hack) with one algorithm both sports call.
public class SeasonWindowResolverTests
{
    private static SeasonWindowResolver.Window Window(int season, DateTime start, DateTime end) =>
        new(season, start, end);

    // -----------------------------------------------------------------------
    // ResolveCurrentWeek — week-level: active window wins, else most-recently-completed,
    // else soonest-upcoming, else null.
    // -----------------------------------------------------------------------

    [Fact]
    public void ResolveCurrentWeek_ReturnsActiveWindow_WhenNowIsWithinOne()
    {
        var now = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
        var windows = new[]
        {
            Window(2026, new DateTime(2026, 10, 1), new DateTime(2026, 10, 8)),
            Window(2026, new DateTime(2026, 10, 8), new DateTime(2026, 10, 20)), // active
            Window(2026, new DateTime(2026, 10, 20), new DateTime(2026, 10, 27)),
        };

        var result = SeasonWindowResolver.ResolveCurrentWeek(windows, now);

        Assert.Equal(windows[1], result);
    }

    [Fact]
    public void ResolveCurrentWeek_ReturnsMostRecentlyCompleted_WhenNoneActive_OffSeason()
    {
        // Now is well after every configured window (off-season) — matches
        // NflCurrentWeekService's existing documented fallback behavior.
        var now = new DateTime(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc);
        var windows = new[]
        {
            Window(2025, new DateTime(2025, 9, 1), new DateTime(2025, 9, 8)),
            Window(2025, new DateTime(2025, 12, 1), new DateTime(2025, 12, 8)), // most recently completed
        };

        var result = SeasonWindowResolver.ResolveCurrentWeek(windows, now);

        Assert.Equal(windows[1], result);
    }

    [Fact]
    public void ResolveCurrentWeek_ReturnsSoonestUpcoming_WhenNoneActiveOrPast_PreSeason()
    {
        var now = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);
        var windows = new[]
        {
            Window(2026, new DateTime(2026, 9, 10), new DateTime(2026, 9, 17)), // soonest upcoming
            Window(2026, new DateTime(2026, 9, 17), new DateTime(2026, 9, 24)),
        };

        var result = SeasonWindowResolver.ResolveCurrentWeek(windows, now);

        Assert.Equal(windows[0], result);
    }

    [Fact]
    public void ResolveCurrentWeek_ReturnsNull_WhenNoWindowsConfigured()
    {
        var result = SeasonWindowResolver.ResolveCurrentWeek([], DateTime.UtcNow);

        Assert.Null(result);
    }

    // -----------------------------------------------------------------------
    // IsSeasonActive — season-level: true within a season's overall span (earliest
    // window Start to latest window End, for that season), regardless of whether any
    // single week window is active at this exact instant.
    // -----------------------------------------------------------------------

    [Fact]
    public void IsSeasonActive_True_DuringAnInSeasonGapBetweenWeeks()
    {
        // The Tuesday between last Monday's game (window 1 already ended) and next
        // Thursday's kickoff (window 2 hasn't started) — no single Window is active, but
        // we are unambiguously still "in season."
        var now = new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc); // Wednesday
        var windows = new[]
        {
            Window(2026, new DateTime(2026, 9, 10), new DateTime(2026, 9, 15)), // ended Tue
            Window(2026, new DateTime(2026, 9, 17), new DateTime(2026, 9, 22)), // starts Thu
        };

        Assert.True(SeasonWindowResolver.IsSeasonActive(windows, now));
    }

    [Fact]
    public void IsSeasonActive_True_ExactlyAtSeasonSpanStart()
    {
        var start = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc);
        var windows = new[] { Window(2026, start, new DateTime(2026, 9, 20)) };

        Assert.True(SeasonWindowResolver.IsSeasonActive(windows, start));
    }

    [Fact]
    public void IsSeasonActive_True_ExactlyAtSeasonSpanEnd()
    {
        var end = new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc);
        var windows = new[] { Window(2026, new DateTime(2026, 9, 10), end) };

        Assert.True(SeasonWindowResolver.IsSeasonActive(windows, end));
    }

    [Fact]
    public void IsSeasonActive_False_OneTickBeforeSeasonSpanStart()
    {
        var start = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc);
        var windows = new[] { Window(2026, start, new DateTime(2026, 9, 20)) };

        Assert.False(SeasonWindowResolver.IsSeasonActive(windows, start.AddTicks(-1)));
    }

    [Fact]
    public void IsSeasonActive_False_OneTickAfterSeasonSpanEnd()
    {
        var end = new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc);
        var windows = new[] { Window(2026, new DateTime(2026, 9, 10), end) };

        Assert.False(SeasonWindowResolver.IsSeasonActive(windows, end.AddTicks(1)));
    }

    [Fact]
    public void IsSeasonActive_False_WellIntoTheOffSeason()
    {
        // This is the exact real bug: today (2026-08-24) is between the 2025 season's end
        // (its Super Bowl / championship, ~Feb 2026) and the 2026 season's start (~Sept
        // 2026) — the poller must not treat this as "in season."
        var now = new DateTime(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc);
        var windows = new[]
        {
            Window(2025, new DateTime(2025, 9, 1), new DateTime(2026, 2, 8)),   // 2025 season, ended Feb 2026
            Window(2026, new DateTime(2026, 9, 10), new DateTime(2027, 2, 7)),  // 2026 season, not started
        };

        Assert.False(SeasonWindowResolver.IsSeasonActive(windows, now));
    }

    [Fact]
    public void IsSeasonActive_False_WhenNoWindowsConfigured()
    {
        Assert.False(SeasonWindowResolver.IsSeasonActive([], DateTime.UtcNow));
    }

    [Fact]
    public void IsSeasonActive_UsesPerSeasonSpan_NotGlobalSpanAcrossAllSeasons()
    {
        // A gap between two DIFFERENT seasons (the actual off-season) must read as
        // inactive even though it falls "between" the earliest and latest window overall
        // — the span must be computed per-season, not across the whole windows list.
        var now = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc); // deep off-season
        var windows = new[]
        {
            Window(2025, new DateTime(2025, 9, 1), new DateTime(2026, 2, 8)),
            Window(2026, new DateTime(2026, 9, 10), new DateTime(2027, 2, 7)),
        };

        Assert.False(SeasonWindowResolver.IsSeasonActive(windows, now));
    }
}
