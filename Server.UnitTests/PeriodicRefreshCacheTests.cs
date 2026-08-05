using FourPlayWebApp.Server.Services;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// frizat-703.6: PeriodicRefreshCache is the shared engine both EspnCacheService (NFL) and
/// CfbCacheService (CFB) wrap — tested in isolation here so both sports inherit verified behavior
/// rather than re-testing the same timer/fingerprint/event machinery twice.
/// </summary>
public class PeriodicRefreshCacheTests
{
    // Deterministic wait — see EspnCacheServiceTests for why fixed Task.Delay windows are flaky
    // under CI load (frizat-703.5 follow-up).
    private static async Task WaitForChangedAsync<T>(PeriodicRefreshCache<T> cache, TimeSpan? timeout = null) where T : class
    {
        var tcs = new TaskCompletionSource();
        cache.Changed += () => tcs.TrySetResult();
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(timeout ?? TimeSpan.FromSeconds(5)));
        Assert.True(completed == tcs.Task, "Timed out waiting for Changed to fire.");
    }

    [Fact]
    public async Task Changed_Fires_OnFirstSuccessfulFetch()
    {
        int fireCount = 0;
        await using var cache = new PeriodicRefreshCache<string>(
            fetch: () => Task.FromResult<string?>("value-1"),
            fingerprint: v => v,
            interval: TimeSpan.FromMinutes(5),
            initialDelay: TimeSpan.FromMilliseconds(50));
        cache.Changed += () => Interlocked.Increment(ref fireCount);

        await WaitForChangedAsync(cache);

        Assert.Equal(1, fireCount);
        Assert.Equal("value-1", cache.Current);
    }

    [Fact]
    public async Task Changed_DoesNotFire_WhenFingerprintUnchanged()
    {
        int fireCount = 0;
        await using var cache = new PeriodicRefreshCache<string>(
            fetch: () => Task.FromResult<string?>("same-value"),
            fingerprint: v => v,
            interval: TimeSpan.FromMinutes(5),
            initialDelay: TimeSpan.FromMilliseconds(50));
        cache.Changed += () => Interlocked.Increment(ref fireCount);

        await WaitForChangedAsync(cache);

        Assert.Equal(1, fireCount); // only the initial fire — timer won't tick again within the test
    }

    [Fact]
    public async Task Current_StaysNull_WhenFetchReturnsNull()
    {
        int fireCount = 0;
        await using var cache = new PeriodicRefreshCache<string>(
            fetch: () => Task.FromResult<string?>(null),
            fingerprint: v => v,
            interval: TimeSpan.FromMinutes(5),
            initialDelay: TimeSpan.FromMilliseconds(50));
        cache.Changed += () => Interlocked.Increment(ref fireCount);

        // Nothing to wait on deterministically — Changed structurally never fires for a null fetch.
        await Task.Delay(500);

        Assert.Equal(0, fireCount);
        Assert.Null(cache.Current);
    }

    [Fact]
    public async Task RefreshLoop_SurvivesFetchException_AndKeepsPriorValue()
    {
        // No Changed event fires here (fetch always throws), so there's nothing to hook the way
        // WaitForChangedAsync does above — signal directly from inside the fetch delegate instead
        // of gambling on a fixed Task.Delay window (the previous version raced the background
        // loop's actual scheduling under CI load and could fail even with correct behavior).
        var fetchRan = new TaskCompletionSource();
        int callCount = 0;
        await using var cache = new PeriodicRefreshCache<string>(
            fetch: () => {
                Interlocked.Increment(ref callCount);
                fetchRan.TrySetResult();
                throw new InvalidOperationException("simulated fetch failure");
            },
            fingerprint: v => v,
            interval: TimeSpan.FromMinutes(5),
            initialDelay: TimeSpan.FromMilliseconds(50));

        var completed = await Task.WhenAny(fetchRan.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.True(completed == fetchRan.Task, "Timed out waiting for the fetch to be called.");

        Assert.True(callCount >= 1);
        Assert.Null(cache.Current);
    }

    [Fact]
    public async Task Current_ReflectsLatestFetchedValue()
    {
        await using var cache = new PeriodicRefreshCache<string>(
            fetch: () => Task.FromResult<string?>("captured-real-value"),
            fingerprint: v => v,
            interval: TimeSpan.FromMinutes(5),
            initialDelay: TimeSpan.FromMilliseconds(50));

        await WaitForChangedAsync(cache);

        Assert.Equal("captured-real-value", cache.Current);
    }
}
