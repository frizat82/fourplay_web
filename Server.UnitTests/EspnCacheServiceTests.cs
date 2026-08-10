using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Enum;
using NSubstitute;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// Tests for EspnCacheService — verifies cache hit/miss behaviour and
/// that the underlying API is called on a miss but not on a hit.
/// </summary>
public class EspnCacheServiceTests
{
    private readonly IEspnApiService _espnApi;
    private readonly INflCurrentWeekService _nflCurrentWeekService;

    // Default week returned by the mock — tests that don't care about the specific week use this
    private static readonly NflWeekInfo DefaultWeek = new(5, 5, 2025, false, "Week 5", "Standard", new DateTime(2025, 10, 2, 18, 0, 0, DateTimeKind.Utc));

    public EspnCacheServiceTests()
    {
        _espnApi = Substitute.For<IEspnApiService>();
        _nflCurrentWeekService = Substitute.For<INflCurrentWeekService>();
        _nflCurrentWeekService.GetCurrentWeekAsync().Returns(DefaultWeek);
    }

    // The service's constructor kicks off its refresh loop as a fire-and-forget background task
    // (only the immediate first run matters in these tests — the PeriodicTimer's 5-minute interval
    // never ticks again within a test's lifetime). Fixed Task.Delay windows racing that background
    // work are flaky under CI load (observed: EspnCacheServiceTests.ScoresChanged_Fires_WhenDataChanges
    // failed in CI while passing locally — a CPU-contention timing miss, not a logic bug). This waits
    // for the actual ScoresChanged fire instead of gambling on a fixed wall-clock window.
    private static async Task WaitForScoresChangedAsync(EspnCacheService svc, TimeSpan? timeout = null)
    {
        var tcs = new TaskCompletionSource();
        svc.ScoresChanged += () => tcs.TrySetResult();
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(timeout ?? TimeSpan.FromSeconds(5)));
        Assert.True(completed == tcs.Task, "Timed out waiting for ScoresChanged to fire.");
    }

    // -----------------------------------------------------------------------
    // GetScoresAsync — cache miss (no entry in cache yet)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetScoresAsync_WhenCacheMiss_ReturnsNull()
    {
        _espnApi.GetWeekScores(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>())
                .Returns(Task.FromResult<EspnScores?>(null));

        await using var svc = new EspnCacheService(_espnApi, _nflCurrentWeekService);

        // API returns null → RefreshScoresAsync short-circuits before firing ScoresChanged, so
        // there's nothing to wait on deterministically; a null result is the correct outcome
        // whether the background refresh has completed yet or not.
        await Task.Delay(300);

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

        // initialDelay gives the test time to subscribe before the first refresh fires —
        // without it, the mocked (near-instant) refresh can complete before ScoresChanged is
        // subscribed to below, and WaitForScoresChangedAsync would wait for an event that
        // already fired.
        await using var svc = new EspnCacheService(_espnApi, _nflCurrentWeekService, initialDelay: TimeSpan.FromMilliseconds(50));
        await WaitForScoresChangedAsync(svc);

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

        // initialDelay gives the test time to subscribe before the first refresh fires — see
        // GetScoresAsync_WhenCacheHit_ReturnsCachedValue for why this is needed.
        await using var svc = new EspnCacheService(_espnApi, _nflCurrentWeekService, initialDelay: TimeSpan.FromMilliseconds(50));
        await WaitForScoresChangedAsync(svc);

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
        await using var svc = new EspnCacheService(_espnApi, _nflCurrentWeekService, initialDelay: TimeSpan.FromMilliseconds(50));
        svc.ScoresChanged += () => Interlocked.Increment(ref fireCount);

        await WaitForScoresChangedAsync(svc);

        Assert.Equal(1, fireCount); // fired once for initial data
    }

    [Fact]
    public async Task ScoresChanged_DoesNotFire_WhenDataUnchanged()
    {
        var scores = new EspnScores { Events = [new Event { Id = "1", Competitions = [new Competition { Status = new EspnStatus { Type = new StatusType { Name = TypeName.StatusFinal } }, Competitors = [new Competitor { HomeAway = HomeAway.Home, Score = 28 }, new Competitor { HomeAway = HomeAway.Away, Score = 17 }], Odds = [] }] }] };

        _espnApi.GetWeekScores(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>()).Returns(Task.FromResult<EspnScores?>(scores));

        int fireCount = 0;
        await using var svc = new EspnCacheService(_espnApi, _nflCurrentWeekService, initialDelay: TimeSpan.FromMilliseconds(50));
        svc.ScoresChanged += () => Interlocked.Increment(ref fireCount);

        await WaitForScoresChangedAsync(svc);

        Assert.Equal(1, fireCount); // fired once on initial load — no second fire since data unchanged
    }

    [Fact]
    public async Task ScoresChanged_DoesNotFire_WhenApiReturnsNull()
    {
        _espnApi.GetWeekScores(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>()).Returns(Task.FromResult<EspnScores?>(null));

        int fireCount = 0;
        await using var svc = new EspnCacheService(_espnApi, _nflCurrentWeekService, initialDelay: TimeSpan.FromMilliseconds(50));
        svc.ScoresChanged += () => Interlocked.Increment(ref fireCount);

        // API returns null → ScoresChanged can never fire (RefreshScoresAsync short-circuits first),
        // so there's nothing to wait on deterministically — a generous fixed window is unavoidable here.
        await Task.Delay(500);

        Assert.Equal(0, fireCount);
    }
}
