namespace FourPlayWebApp.Server.Services;

/// <summary>
/// Generic "poll fast during an active window, slow otherwise" interval calculator for
/// PeriodicRefreshCache&lt;T&gt;. Provider-agnostic by design — takes a caller-supplied selector
/// that extracts the relevant active-window start times out of whatever T a specific cache holds
/// (e.g. EspnCacheService/CfbCacheService's EspnScores kickoff times today). A future data source
/// with its own response shape reuses this unchanged; only its own selector differs.
/// </summary>
public static class AdaptivePollInterval
{
    public static TimeSpan Compute<T>(
        T? current,
        Func<T, IEnumerable<DateTimeOffset>> activeWindowStarts,
        TimeSpan activeWindowDuration,
        TimeSpan fastInterval,
        TimeSpan slowInterval,
        DateTimeOffset now) where T : class
    {
        if (current is null) return slowInterval;
        var isActive = activeWindowStarts(current).Any(start => IsInWindow(start, activeWindowDuration, now));
        return isActive ? fastInterval : slowInterval;
    }

    /// <summary>The one "is this window active" rule — also what LiveDayRefresh calls a live game.</summary>
    public static bool IsInWindow(DateTimeOffset start, TimeSpan duration, DateTimeOffset now) =>
        start <= now && now <= start + duration;
}
