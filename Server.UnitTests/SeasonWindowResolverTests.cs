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

    private static SeasonWindowResolver.WeekWindow WeekWindow(int season, DateTime start, DateTime end, DateTime spreadLock) =>
        new(season, start, end, spreadLock);

    // -----------------------------------------------------------------------
    // ResolveCurrentWeek — week-level (frizat-9xg): "current" is the most recent window
    // whose own SpreadLockDatetime has passed (real odds/results exist), UNLESS we're
    // within 2 days of the NEXT window's own SpreadLockDatetime, in which case that next
    // window becomes current early — even before its own calendar Start, and even before
    // it has any odds posted yet (the frontend's SpreadRelease/selector fix handles that
    // display state). Applies identically whether "next" is a later week in the same
    // season or week 1 of a new season — no season-boundary special case.
    // -----------------------------------------------------------------------

    [Fact]
    public void ResolveCurrentWeek_ReturnsLastStartedWindow_WhenMoreThanTwoDaysBeforeNextSpreadLock()
    {
        var now = new DateTime(2026, 9, 12, 12, 0, 0, DateTimeKind.Utc); // 5 days before week 2's spread lock
        var windows = new[]
        {
            WeekWindow(2026, new DateTime(2026, 9, 8), new DateTime(2026, 9, 15), new DateTime(2026, 9, 9)), // started
            WeekWindow(2026, new DateTime(2026, 9, 15), new DateTime(2026, 9, 22), new DateTime(2026, 9, 17)), // not yet
        };

        var result = SeasonWindowResolver.ResolveCurrentWeek(windows, now);

        Assert.Equal(windows[0], result);
    }

    [Fact]
    public void ResolveCurrentWeek_ReturnsNextWindow_WhenWithinTwoDaysOfItsSpreadLock()
    {
        var now = new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc); // 1.x days before week 2's spread lock
        var windows = new[]
        {
            WeekWindow(2026, new DateTime(2026, 9, 8), new DateTime(2026, 9, 15), new DateTime(2026, 9, 9)),
            WeekWindow(2026, new DateTime(2026, 9, 15), new DateTime(2026, 9, 22), new DateTime(2026, 9, 17)), // early-activates
        };

        var result = SeasonWindowResolver.ResolveCurrentWeek(windows, now);

        Assert.Equal(windows[1], result);
    }

    [Fact]
    public void ResolveCurrentWeek_ReturnsNextWindow_ExactlyAtTwoDayBoundary()
    {
        var nextSpreadLock = new DateTime(2026, 9, 17, 0, 0, 0, DateTimeKind.Utc);
        var now = nextSpreadLock.AddDays(-2); // exactly at the boundary — inclusive
        var windows = new[]
        {
            WeekWindow(2026, new DateTime(2026, 9, 8), new DateTime(2026, 9, 15), new DateTime(2026, 9, 9)),
            WeekWindow(2026, new DateTime(2026, 9, 15), new DateTime(2026, 9, 22), nextSpreadLock),
        };

        var result = SeasonWindowResolver.ResolveCurrentWeek(windows, now);

        Assert.Equal(windows[1], result);
    }

    [Fact]
    public void ResolveCurrentWeek_ReturnsLastStartedWindow_OneTickBeforeTwoDayBoundary()
    {
        var nextSpreadLock = new DateTime(2026, 9, 17, 0, 0, 0, DateTimeKind.Utc);
        var now = nextSpreadLock.AddDays(-2).AddTicks(-1); // one tick short of the boundary
        var windows = new[]
        {
            WeekWindow(2026, new DateTime(2026, 9, 8), new DateTime(2026, 9, 15), new DateTime(2026, 9, 9)),
            WeekWindow(2026, new DateTime(2026, 9, 15), new DateTime(2026, 9, 22), nextSpreadLock),
        };

        var result = SeasonWindowResolver.ResolveCurrentWeek(windows, now);

        Assert.Equal(windows[0], result);
    }

    [Fact]
    public void ResolveCurrentWeek_SeasonBoundaryTransition_BehavesIdenticallyToInSeasonTransition()
    {
        // Real prod shape: 2025 season's last window (Super Bowl) ended months ago; 2026
        // week 1's spread lock is still >2 days out — must show 2025's last window, exactly
        // like an ordinary in-season week-to-week gap. No season-aware branching.
        var now = new DateTime(2026, 8, 27, 12, 0, 0, DateTimeKind.Utc);
        var windows = new[]
        {
            WeekWindow(2025, new DateTime(2026, 2, 3), new DateTime(2026, 2, 10), new DateTime(2026, 2, 5)), // last started
            WeekWindow(2026, new DateTime(2026, 9, 8), new DateTime(2026, 9, 15), new DateTime(2026, 9, 9)), // 13 days out
        };

        var result = SeasonWindowResolver.ResolveCurrentWeek(windows, now);

        Assert.Equal(windows[0], result);

        // Now within 2 days of the new season's week 1 spread lock — same rule flips it over.
        var closeNow = new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(windows[1], SeasonWindowResolver.ResolveCurrentWeek(windows, closeNow));
    }

    [Fact]
    public void ResolveCurrentWeek_ReturnsSoonestUpcoming_WhenNothingHasEverStarted()
    {
        // Bootstrap case (app's very first-ever season, before any spread has ever been
        // grabbed) — preserves the original "soonest upcoming" fallback regardless of the
        // 2-day proximity rule, since there is no "last started" window to prefer instead.
        var now = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);
        var windows = new[]
        {
            WeekWindow(2026, new DateTime(2026, 9, 10), new DateTime(2026, 9, 17), new DateTime(2026, 9, 11)), // soonest upcoming
            WeekWindow(2026, new DateTime(2026, 9, 17), new DateTime(2026, 9, 24), new DateTime(2026, 9, 18)),
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
