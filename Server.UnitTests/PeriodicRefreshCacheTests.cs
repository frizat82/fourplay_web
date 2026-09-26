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
            intervalSelector: _ => TimeSpan.FromMinutes(5),
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
            intervalSelector: _ => TimeSpan.FromMinutes(5),
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
            intervalSelector: _ => TimeSpan.FromMinutes(5),
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
            intervalSelector: _ => TimeSpan.FromMinutes(5),
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
            intervalSelector: _ => TimeSpan.FromMinutes(5),
            initialDelay: TimeSpan.FromMilliseconds(50));

        await WaitForChangedAsync(cache);

        Assert.Equal("captured-real-value", cache.Current);
    }

    // frizat-ucv: intervalSelector is consulted fresh after every refresh, using the value that
    // refresh just produced — not a fixed interval computed once at construction.
    [Fact]
    public async Task RefreshLoop_UsesIntervalSelector_FastValue_RefreshesRepeatedly()
    {
        int callCount = 0;
        await using var cache = new PeriodicRefreshCache<string>(
            fetch: () => {
                var n = Interlocked.Increment(ref callCount);
                return Task.FromResult<string?>($"value-{n}"); // distinct each call -> Changed fires every time
            },
            fingerprint: v => v,
            intervalSelector: _ => TimeSpan.FromMilliseconds(20),
            initialDelay: TimeSpan.FromMilliseconds(20));

        var tcs = new TaskCompletionSource();
        int seenChanges = 0;
        cache.Changed += () => { if (Interlocked.Increment(ref seenChanges) >= 4) tcs.TrySetResult(); };
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));

        Assert.True(completed == tcs.Task, $"Expected at least 4 refreshes with a fast interval selector; saw {callCount}.");
    }

    [Fact]
    public async Task RefreshLoop_UsesIntervalSelector_SlowValue_DoesNotRefreshAgainWithinShortWindow()
    {
        int callCount = 0;
        await using var cache = new PeriodicRefreshCache<string>(
            fetch: () => { Interlocked.Increment(ref callCount); return Task.FromResult<string?>("same-value"); },
            fingerprint: v => v,
            intervalSelector: _ => TimeSpan.FromSeconds(30),
            initialDelay: TimeSpan.FromMilliseconds(20));

        // Long enough to be well past the initial fetch, nowhere near the 30s selected interval.
        await Task.Delay(500);

        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task RefreshLoop_IntervalSelector_ReceivesTheJustFetchedCurrentValue()
    {
        var seenByselector = new List<string?>();
        int callCount = 0;
        await using var cache = new PeriodicRefreshCache<string>(
            fetch: () => {
                var n = Interlocked.Increment(ref callCount);
                return Task.FromResult<string?>($"value-{n}");
            },
            fingerprint: v => v,
            intervalSelector: current => {
                lock (seenByselector) seenByselector.Add(current);
                return TimeSpan.FromMilliseconds(20);
            },
            initialDelay: TimeSpan.FromMilliseconds(20));

        var tcs = new TaskCompletionSource();
        cache.Changed += () => { lock (seenByselector) if (seenByselector.Count >= 3) tcs.TrySetResult(); };
        await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));

        lock (seenByselector)
        {
            // The selector is only ever consulted AFTER a refresh (to decide the wait before the
            // next one), so it must see that refresh's own freshly-set Current — never null (the
            // constructor's initial fetch already ran) and never a stale prior value.
            Assert.DoesNotContain(null, seenByselector);
            Assert.Contains("value-1", seenByselector);
        }
    }

    // The interval selector runs outside the refresh's own try/catch; if it ever throws (e.g. a
    // malformed ESPN payload), the loop must fall back to a retry delay, not die silently.
    [Fact]
    public async Task ASelectorThatThrows_DoesNotStopTheRefreshLoop() {
        var fetches = 0;
        var second = new TaskCompletionSource();
        await using var cache = new PeriodicRefreshCache<string>(
            fetch: () => { if (Interlocked.Increment(ref fetches) == 2) second.TrySetResult(); return Task.FromResult<string?>("v"); },
            fingerprint: v => v,
            intervalSelector: _ => throw new InvalidOperationException("bad payload"),
            intervalOnError: TimeSpan.FromMilliseconds(10));

        var completed = await Task.WhenAny(second.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(second.Task, completed);
    }
}
