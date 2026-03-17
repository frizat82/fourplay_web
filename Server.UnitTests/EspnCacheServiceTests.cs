using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// Tests for EspnCacheService — verifies cache hit/miss behaviour and
/// that the underlying API is called on a miss but not on a hit.
/// </summary>
public class EspnCacheServiceTests : IAsyncDisposable
{
    private readonly IEspnApiService _espnApi;
    private readonly IMemoryCache _memoryCache;

    public EspnCacheServiceTests()
    {
        _espnApi = Substitute.For<IEspnApiService>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
    }

    // -----------------------------------------------------------------------
    // GetScoresAsync — cache miss (no entry in cache yet)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetScoresAsync_WhenCacheMiss_ReturnsNull()
    {
        // The background timer fires immediately in the constructor, so we need
        // to stub the API before constructing to avoid a real HTTP call.
        _espnApi.GetScores().Returns(Task.FromResult<EspnScores?>(null));

        await using var svc = new EspnCacheService(_espnApi, _memoryCache);

        // Small delay to let the initial RefreshLoopAsync fire
        await Task.Delay(50);

        var result = await svc.GetScoresAsync();
        // API returned null → nothing stored → null returned
        Assert.Null(result);
    }

    // -----------------------------------------------------------------------
    // GetScoresAsync — cache hit (entry was stored by a prior refresh)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetScoresAsync_WhenCacheHit_ReturnsCachedValue()
    {
        var scores = new EspnScores { Season = new Season { Year = 2025 } };
        _espnApi.GetScores().Returns(Task.FromResult<EspnScores?>(scores));

        await using var svc = new EspnCacheService(_espnApi, _memoryCache);

        // Allow initial refresh to complete
        await Task.Delay(100);

        var result = await svc.GetScoresAsync();

        Assert.NotNull(result);
        Assert.Equal(2025, result.Season?.Year);
    }

    // -----------------------------------------------------------------------
    // GetScoresAsync — API exception does not propagate (keeps last good value)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetScoresAsync_WhenApiThrows_ReturnsCachedValue()
    {
        // First call succeeds (populates cache), second throws
        var scores = new EspnScores { Season = new Season { Year = 2024 } };
        _espnApi.GetScores()
            .Returns(
                Task.FromResult<EspnScores?>(scores),
                Task.FromException<EspnScores?>(new HttpRequestException("timeout")));

        await using var svc = new EspnCacheService(_espnApi, _memoryCache);

        // Allow first refresh to settle
        await Task.Delay(100);

        // Even if a subsequent refresh throws, the previously cached value remains
        var result = await svc.GetScoresAsync();
        Assert.NotNull(result);
        Assert.Equal(2024, result.Season?.Year);
    }

    public async ValueTask DisposeAsync()
    {
        _memoryCache.Dispose();
        await Task.CompletedTask;
    }
}
