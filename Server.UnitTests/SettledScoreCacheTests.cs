using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Shared.Models;
using Microsoft.Extensions.Caching.Memory;

namespace FourPlayWebApp.Server.UnitTests;

// frizat-d0t: the shared cache-or-build orchestration extracted out of EspnCacheService.
// GetWeekScoresAsync (NFL) and CfbLiveScoreFetcher.FetchForSlateAsync's settled-cache branch
// (CFB) — window-ended detection, current-item exemption, cache hit/miss, and DB-build-then-cache
// are each tested once here instead of twice (once per sport). Callers (EspnCacheService,
// CfbCacheService) still own resolving isCurrentItem/windowEndUtc themselves, AND building an
// EspnScores from persisted rows (FinalScoresEspnMapper.Build + their own sport-specific row
// mapping) — this class only orchestrates what happens once those are known, deliberately with
// no knowledge of FinalScoresEspnMapper/FinishedGame at all.
public class SettledScoreCacheTests {
    private readonly IMemoryCache _memoryCache = new MemoryCache(new MemoryCacheOptions());
    private SettledScoreCache Build() => new(_memoryCache);

    private static EspnScores MakeBuiltScores() => new() { Season = new Season { Year = 2025 } };

    [Fact]
    public async Task GetOrBuildAsync_CacheHit_ReturnsCachedValue_NeverCallsEitherDelegate() {
        var cache = Build();
        var cached = new EspnScores { Season = new Season { Year = 2025 } };
        _memoryCache.Set("key-1", cached);

        var buildCalled = false;
        var liveFetchCalled = false;
        var result = await cache.GetOrBuildAsync(
            "key-1", isCurrentItem: false, windowEndUtc: DateTime.UtcNow.AddDays(-30),
            buildFromRowsAsync: () => { buildCalled = true; return Task.FromResult<EspnScores?>(null); },
            liveFetchAsync: () => { liveFetchCalled = true; return Task.FromResult<EspnScores?>(null); });

        Assert.Same(cached, result);
        Assert.False(buildCalled);
        Assert.False(liveFetchCalled);
    }

    [Fact]
    public async Task GetOrBuildAsync_WindowNotEnded_CallsLiveFetch_NeverCallsBuild() {
        var cache = Build();
        var buildCalled = false;
        var live = new EspnScores { Season = new Season { Year = 2025 } };

        var result = await cache.GetOrBuildAsync(
            "key-2", isCurrentItem: false, windowEndUtc: DateTime.UtcNow.AddDays(1),
            buildFromRowsAsync: () => { buildCalled = true; return Task.FromResult<EspnScores?>(MakeBuiltScores()); },
            liveFetchAsync: () => Task.FromResult<EspnScores?>(live));

        Assert.Same(live, result);
        Assert.False(buildCalled);
    }

    [Fact]
    public async Task GetOrBuildAsync_WindowEndedWithABuild_CachesIt_NeverCallsLiveFetch() {
        var cache = Build();
        var liveFetchCalled = false;
        var built = MakeBuiltScores();

        var result = await cache.GetOrBuildAsync(
            "key-3", isCurrentItem: false, windowEndUtc: DateTime.UtcNow.AddDays(-30),
            buildFromRowsAsync: () => Task.FromResult<EspnScores?>(built),
            liveFetchAsync: () => { liveFetchCalled = true; return Task.FromResult<EspnScores?>(null); });

        Assert.Same(built, result);
        Assert.False(liveFetchCalled);
        Assert.True(_memoryCache.TryGetValue<EspnScores>("key-3", out var cached));
        Assert.Same(built, cached);
    }

    [Fact]
    public async Task GetOrBuildAsync_WindowEndedWithNoBuild_FallsBackToLiveFetch() {
        var cache = Build();
        var live = new EspnScores { Season = new Season { Year = 2025 } };

        var result = await cache.GetOrBuildAsync(
            "key-4", isCurrentItem: false, windowEndUtc: DateTime.UtcNow.AddDays(-30),
            buildFromRowsAsync: () => Task.FromResult<EspnScores?>(null),
            liveFetchAsync: () => Task.FromResult<EspnScores?>(live));

        Assert.Same(live, result);
        // A null build (e.g. zero persisted rows) is never cached — a still-active item that
        // merely hasn't had any games persisted yet must not freeze an empty response in place
        // once games actually finish and get persisted.
        Assert.False(_memoryCache.TryGetValue<EspnScores>("key-4", out _));
    }

    [Fact]
    public async Task GetOrBuildAsync_IsCurrentItem_AlwaysCallsLiveFetch_EvenWithEndedWindowAndABuild() {
        var cache = Build();
        var buildCalled = false;
        var live = new EspnScores { Season = new Season { Year = 2025 } };

        var result = await cache.GetOrBuildAsync(
            "key-5", isCurrentItem: true, windowEndUtc: DateTime.UtcNow.AddDays(-30),
            buildFromRowsAsync: () => { buildCalled = true; return Task.FromResult<EspnScores?>(MakeBuiltScores()); },
            liveFetchAsync: () => Task.FromResult<EspnScores?>(live));

        Assert.Same(live, result);
        Assert.False(buildCalled);
    }

    [Fact]
    public void TryGet_ReturnsTrueAndValue_OnlyForAnAlreadyCachedKey() {
        var cache = Build();
        var cached = new EspnScores { Season = new Season { Year = 2025 } };
        _memoryCache.Set("key-6", cached);

        Assert.True(cache.TryGet("key-6", out var hit));
        Assert.Same(cached, hit);
        Assert.False(cache.TryGet("key-not-set", out var miss));
        Assert.Null(miss);
    }

    [Fact]
    public void Invalidate_RemovesOnlyTheSpecifiedKey() {
        var cache = Build();
        _memoryCache.Set("key-a", new EspnScores());
        _memoryCache.Set("key-b", new EspnScores());

        cache.Invalidate("key-a");

        Assert.False(_memoryCache.TryGetValue<EspnScores>("key-a", out _));
        Assert.True(_memoryCache.TryGetValue<EspnScores>("key-b", out _));
    }
}
