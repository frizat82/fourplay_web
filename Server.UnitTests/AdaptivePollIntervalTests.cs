using FourPlayWebApp.Server.Services;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// frizat-ucv: generic "poll fast during an active window, slow otherwise" calculator for
/// PeriodicRefreshCache. Provider-agnostic by design — takes a caller-supplied selector to pull
/// the relevant window-start timestamps out of whatever T a specific cache holds, so a future
/// non-ESPN data source can reuse this without any change here, only its own selector.
/// </summary>
public class AdaptivePollIntervalTests
{
    private static readonly TimeSpan Duration = TimeSpan.FromHours(4);
    private static readonly TimeSpan Fast = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Slow = TimeSpan.FromMinutes(5);

    [Fact]
    public void Compute_ReturnsSlow_WhenCurrentIsNull()
    {
        var result = AdaptivePollInterval.Compute<string>(
            current: null,
            activeWindowStarts: _ => [],
            Duration, Fast, Slow, DateTimeOffset.UtcNow);

        Assert.Equal(Slow, result);
    }

    [Fact]
    public void Compute_ReturnsSlow_WhenNoWindowsExist()
    {
        var result = AdaptivePollInterval.Compute(
            current: "anything",
            activeWindowStarts: _ => [],
            Duration, Fast, Slow, DateTimeOffset.UtcNow);

        Assert.Equal(Slow, result);
    }

    [Fact]
    public void Compute_ReturnsFast_WhenNowIsInsideAWindow()
    {
        var now = DateTimeOffset.UtcNow;
        var start = now.AddHours(-1); // 1h into a 4h window

        var result = AdaptivePollInterval.Compute(
            current: "anything",
            activeWindowStarts: _ => [start],
            Duration, Fast, Slow, now);

        Assert.Equal(Fast, result);
    }

    [Fact]
    public void Compute_ReturnsSlow_BeforeWindowStarts()
    {
        var now = DateTimeOffset.UtcNow;
        var start = now.AddMinutes(30); // starts in the future

        var result = AdaptivePollInterval.Compute(
            current: "anything",
            activeWindowStarts: _ => [start],
            Duration, Fast, Slow, now);

        Assert.Equal(Slow, result);
    }

    [Fact]
    public void Compute_ReturnsSlow_AfterWindowElapses()
    {
        var now = DateTimeOffset.UtcNow;
        var start = now.Subtract(Duration).AddMinutes(-1); // window ended 1 minute ago

        var result = AdaptivePollInterval.Compute(
            current: "anything",
            activeWindowStarts: _ => [start],
            Duration, Fast, Slow, now);

        Assert.Equal(Slow, result);
    }

    [Theory]
    [InlineData(0)]     // exactly at window start
    [InlineData(1)]     // exactly at window end (start + duration)
    public void Compute_TreatsWindowBoundaries_AsInclusive(int hoursFromStart)
    {
        var now = DateTimeOffset.UtcNow;
        var start = now.AddHours(-hoursFromStart);

        var result = AdaptivePollInterval.Compute(
            current: "anything",
            activeWindowStarts: _ => [start],
            Duration, Fast, Slow, now);

        Assert.Equal(Fast, result);
    }

    [Fact]
    public void Compute_ReturnsFast_WhenAnyOfMultipleWindowsIsActive()
    {
        var now = DateTimeOffset.UtcNow;
        var irrelevant = now.AddDays(-3);
        var active = now.AddMinutes(-10);

        var result = AdaptivePollInterval.Compute(
            current: "anything",
            activeWindowStarts: _ => [irrelevant, active],
            Duration, Fast, Slow, now);

        Assert.Equal(Fast, result);
    }
}
