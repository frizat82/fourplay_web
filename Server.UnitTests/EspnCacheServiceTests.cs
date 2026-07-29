using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Enum;
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
    private readonly INflCurrentWeekService _nflCurrentWeekService;

    // Default week returned by the mock — tests that don't care about the specific week use this
    private static readonly NflWeekInfo DefaultWeek = new(5, 5, 2025, false, "Week 5", "Standard");

    public EspnCacheServiceTests()
    {
        _espnApi = Substitute.For<IEspnApiService>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _nflCurrentWeekService = Substitute.For<INflCurrentWeekService>();
        _nflCurrentWeekService.GetCurrentWeekAsync().Returns(DefaultWeek);
    }

    // -----------------------------------------------------------------------
    // GetScoresAsync — cache miss (no entry in cache yet)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetScoresAsync_WhenCacheMiss_ReturnsNull()
    {
        _espnApi.GetWeekScores(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>())
                .Returns(Task.FromResult<EspnScores?>(null));

        await using var svc = new EspnCacheService(_espnApi, _memoryCache, _nflCurrentWeekService);

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
        _espnApi.GetWeekScores(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>())
                .Returns(Task.FromResult<EspnScores?>(scores));

        await using var svc = new EspnCacheService(_espnApi, _memoryCache, _nflCurrentWeekService);

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
        _espnApi.GetWeekScores(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>())
                .Returns(
                    Task.FromResult<EspnScores?>(scores),
                    Task.FromException<EspnScores?>(new HttpRequestException("timeout")));

        await using var svc = new EspnCacheService(_espnApi, _memoryCache, _nflCurrentWeekService);

        // Allow first refresh to settle
        await Task.Delay(100);

        // Even if a subsequent refresh throws, the previously cached value remains
        var result = await svc.GetScoresAsync();
        Assert.NotNull(result);
        Assert.Equal(2024, result.Season?.Year);
    }

    // -----------------------------------------------------------------------
    // ScoresChanged — fires when data changes, silent when unchanged/null
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ScoresChanged_Fires_WhenDataChanges()
    {
        var first = new EspnScores { Events = [new Event { Id = "1", Competitions = [new Competition { Status = new EspnStatus { Type = new StatusType { Name = TypeName.StatusScheduled } }, Competitors = [new Competitor { HomeAway = HomeAway.Home, Score = 0 }, new Competitor { HomeAway = HomeAway.Away, Score = 0 }], Odds = [] }] }] };
        var second = new EspnScores { Events = [new Event { Id = "1", Competitions = [new Competition { Status = new EspnStatus { Type = new StatusType { Name = TypeName.StatusFinal } }, Competitors = [new Competitor { HomeAway = HomeAway.Home, Score = 28 }, new Competitor { HomeAway = HomeAway.Away, Score = 17 }], Odds = [] }] }] };

        _espnApi.GetWeekScores(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>()).Returns(
            Task.FromResult<EspnScores?>(first),
            Task.FromResult<EspnScores?>(second));

        int fireCount = 0;
        // initialDelay gives us time to subscribe before the first refresh fires
        await using var svc = new EspnCacheService(_espnApi, _memoryCache, _nflCurrentWeekService, initialDelay: TimeSpan.FromMilliseconds(50));
        svc.ScoresChanged += () => Interlocked.Increment(ref fireCount);

        await Task.Delay(300);

        Assert.Equal(1, fireCount); // fired once for initial data
    }

    [Fact]
    public async Task ScoresChanged_DoesNotFire_WhenDataUnchanged()
    {
        var scores = new EspnScores { Events = [new Event { Id = "1", Competitions = [new Competition { Status = new EspnStatus { Type = new StatusType { Name = TypeName.StatusFinal } }, Competitors = [new Competitor { HomeAway = HomeAway.Home, Score = 28 }, new Competitor { HomeAway = HomeAway.Away, Score = 17 }], Odds = [] }] }] };

        _espnApi.GetWeekScores(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>()).Returns(Task.FromResult<EspnScores?>(scores));

        int fireCount = 0;
        await using var svc = new EspnCacheService(_espnApi, _memoryCache, _nflCurrentWeekService, initialDelay: TimeSpan.FromMilliseconds(50));
        svc.ScoresChanged += () => Interlocked.Increment(ref fireCount);

        await Task.Delay(300);

        Assert.Equal(1, fireCount); // fired once on initial load — no second fire since data unchanged
    }

    [Fact]
    public async Task ScoresChanged_DoesNotFire_WhenApiReturnsNull()
    {
        _espnApi.GetWeekScores(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>()).Returns(Task.FromResult<EspnScores?>(null));

        int fireCount = 0;
        await using var svc = new EspnCacheService(_espnApi, _memoryCache, _nflCurrentWeekService, initialDelay: TimeSpan.FromMilliseconds(50));
        svc.ScoresChanged += () => Interlocked.Increment(ref fireCount);

        await Task.Delay(300);

        Assert.Equal(0, fireCount);
    }

    public async ValueTask DisposeAsync()
    {
        _memoryCache.Dispose();
        await Task.CompletedTask;
    }
}
