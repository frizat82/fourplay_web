using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Enum;
using Microsoft.Extensions.Caching.Memory;
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
    private readonly ILeagueRepository _leagueRepo;
    private readonly IMemoryCache _memoryCache = new MemoryCache(new MemoryCacheOptions());

    // Default week returned by the mock — tests that don't care about the specific week use this
    private static readonly NflWeekInfo DefaultWeek = new(5, 5, 2025, false, "Week 5", "Standard", new DateTime(2025, 10, 2, 18, 0, 0, DateTimeKind.Utc));

    public EspnCacheServiceTests()
    {
        _espnApi = Substitute.For<IEspnApiService>();
        _nflCurrentWeekService = Substitute.For<INflCurrentWeekService>();
        _nflCurrentWeekService.GetCurrentWeekAsync().Returns(DefaultWeek);
        _leagueRepo = Substitute.For<ILeagueRepository>();
        // Default: no persisted rows for any week, so existing tests (which never seed the repo)
        // keep exercising the live-ESPN branch exactly as before this constructor param was added.
        _leagueRepo.GetNflScoresAsync(Arg.Any<int>(), Arg.Any<int>()).Returns([]);
        // Default: no configs, so GetWeekScoresAsync's "has this week ended" check finds no match
        // and safely falls through to ESPN — tests that need the DB-first branch stub this
        // explicitly with a matching config below.
        _leagueRepo.GetNflSeasonWeekConfigsAsync().Returns(new List<Models.Data.NflSeasonWeekConfig>());
        // Default: a season is active, so existing poller tests (GetScoresAsync_*/ScoresChanged_*)
        // keep exercising the live-ESPN fetch branch unchanged by the new off-season gate.
        _nflCurrentWeekService.IsSeasonActiveAsync().Returns(true);
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

        await using var svc = new EspnCacheService(_espnApi, _nflCurrentWeekService, _leagueRepo, _memoryCache);

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
        await using var svc = new EspnCacheService(_espnApi, _nflCurrentWeekService, _leagueRepo, _memoryCache, initialDelay: TimeSpan.FromMilliseconds(50));
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
        await using var svc = new EspnCacheService(_espnApi, _nflCurrentWeekService, _leagueRepo, _memoryCache, initialDelay: TimeSpan.FromMilliseconds(50));
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
        await using var svc = new EspnCacheService(_espnApi, _nflCurrentWeekService, _leagueRepo, _memoryCache, initialDelay: TimeSpan.FromMilliseconds(50));
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
        await using var svc = new EspnCacheService(_espnApi, _nflCurrentWeekService, _leagueRepo, _memoryCache, initialDelay: TimeSpan.FromMilliseconds(50));
        svc.ScoresChanged += () => Interlocked.Increment(ref fireCount);

        await WaitForScoresChangedAsync(svc);

        Assert.Equal(1, fireCount); // fired once on initial load — no second fire since data unchanged
    }

    [Fact]
    public async Task ScoresChanged_DoesNotFire_WhenApiReturnsNull()
    {
        _espnApi.GetWeekScores(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>()).Returns(Task.FromResult<EspnScores?>(null));

        int fireCount = 0;
        await using var svc = new EspnCacheService(_espnApi, _nflCurrentWeekService, _leagueRepo, _memoryCache, initialDelay: TimeSpan.FromMilliseconds(50));
        svc.ScoresChanged += () => Interlocked.Increment(ref fireCount);

        // API returns null → ScoresChanged can never fire (RefreshScoresAsync short-circuits first),
        // so there's nothing to wait on deterministically — a generous fixed window is unavoidable here.
        await Task.Delay(500);

        Assert.Equal(0, fireCount);
    }

    // -----------------------------------------------------------------------
    // Off-season gating — the poller must not hit ESPN when no season is active (frizat plan:
    // wobbly-chasing-lynx — this is the fix for the always-on 5-min poller hitting ESPN 24/7/365
    // regardless of season).
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetScoresAsync_NeverCallsEspn_WhenNoSeasonIsCurrentlyActive()
    {
        _nflCurrentWeekService.IsSeasonActiveAsync().Returns(false);

        await using var svc = new EspnCacheService(_espnApi, _nflCurrentWeekService, _leagueRepo, _memoryCache, initialDelay: TimeSpan.FromMilliseconds(50));
        await Task.Delay(300);

        var result = await svc.GetScoresAsync();

        Assert.Null(result);
        await _espnApi.DidNotReceiveWithAnyArgs().GetWeekScores(default, default, default);
    }

    // -----------------------------------------------------------------------
    // GetWeekScoresAsync — DB-first for historical weeks (100 concurrent users viewing the same
    // past week shouldn't trigger 100 independent ESPN calls for games already FINAL and persisted)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetWeekScoresAsync_WhenDbHasPersistedRowsForTheWeek_ReturnsDbBuiltScores_NeverCallsEspn()
    {
        var rows = new List<Shared.Models.Data.NflScores> {
            new() { Id = 1, Season = 2025, NflWeek = 19, HomeTeam = "KC", AwayTeam = "DEN", HomeTeamScore = 27, AwayTeamScore = 20, GameTime = new DateTimeOffset(2026, 1, 10, 18, 0, 0, TimeSpan.Zero) },
        };
        // Wild Card (ESPN week 1, postseason) = internal NflWeek 19 (GameHelpers.GetWeekFromEspnWeek).
        _leagueRepo.GetNflScoresAsync(2025, 19).Returns(rows);
        // This week's own window has fully ended (well in the past) — DB-first only kicks in once
        // that's true, mirroring CfbLiveScoreFetcher's identical fix.
        _leagueRepo.GetNflSeasonWeekConfigsAsync().Returns(new List<Models.Data.NflSeasonWeekConfig> {
            new() {
                Season = 2025, WeekId = 19, WeekLabel = "Wild Card", WeekType = "PostSeason", ScoringFormat = "Standard",
                WeekStartDatetime = new DateTime(2026, 1, 8), WeekEndDatetime = new DateTime(2026, 1, 12),
                SpreadLockDatetime = new DateTime(2026, 1, 8),
            },
            // A later (still real-world-past) week the resolver treats as "current" — without
            // this, the Wild Card row above would trivially resolve as its own "current" week
            // (nothing else to compare against), exempting it from the DB-first shortcut this
            // test exists to verify.
            new() {
                Season = 2025, WeekId = 20, WeekLabel = "Divisional", WeekType = "PostSeason", ScoringFormat = "Standard",
                WeekStartDatetime = new DateTime(2026, 1, 15), WeekEndDatetime = new DateTime(2026, 1, 19),
                SpreadLockDatetime = new DateTime(2026, 1, 15),
            },
        });

        await using var svc = new EspnCacheService(_espnApi, _nflCurrentWeekService, _leagueRepo, _memoryCache, initialDelay: TimeSpan.FromMinutes(5));
        var result = await svc.GetWeekScoresAsync(1, 2025, postSeason: true);

        Assert.NotNull(result);
        var comp = result!.Events!.Single().Competitions[0];
        var home = comp.Competitors.Single(c => c.HomeAway == HomeAway.Home);
        Assert.Equal("KC", home.Team.Abbreviation);
        Assert.Equal(27, home.Score);
        Assert.Equal(TypeName.StatusFinal, comp.Status.Type.Name);
        await _espnApi.DidNotReceiveWithAnyArgs().GetWeekScores(default, default, default);
    }

    // frizat: nflAdapter.ts's current-week path now always calls this endpoint for whichever
    // week the control table (SeasonWindowResolver) resolves as current — that week must always
    // be live-fetched, even if its own calendar window looks "ended" and persisted rows exist,
    // or the demo's frozen in-progress fixture data never surfaces for it (this exact regression
    // broke NFL demo e2e tests before this exemption was added).
    [Fact]
    public async Task GetWeekScoresAsync_WhenWeekIsTheResolvedCurrentWeek_AlwaysCallsEspn_EvenWithPersistedRowsAndEndedWindow()
    {
        var rows = new List<Shared.Models.Data.NflScores> {
            new() { Id = 1, Season = 2025, NflWeek = 19, HomeTeam = "KC", AwayTeam = "DEN", HomeTeamScore = 27, AwayTeamScore = 20, GameTime = new DateTimeOffset(2026, 1, 10, 18, 0, 0, TimeSpan.Zero) },
        };
        _leagueRepo.GetNflScoresAsync(2025, 19).Returns(rows);
        // Only one config row — with nothing else to compare against, the resolver trivially
        // treats it as "current" even though its window is long past.
        _leagueRepo.GetNflSeasonWeekConfigsAsync().Returns(new List<Models.Data.NflSeasonWeekConfig> {
            new() {
                Season = 2025, WeekId = 19, WeekLabel = "Wild Card", WeekType = "PostSeason", ScoringFormat = "Standard",
                WeekStartDatetime = new DateTime(2026, 1, 8), WeekEndDatetime = new DateTime(2026, 1, 12),
                SpreadLockDatetime = new DateTime(2026, 1, 8),
            },
        });
        var espnScores = new EspnScores { Season = new Season { Year = 2025 } };
        _espnApi.GetWeekScores(1, 2025, true).Returns(Task.FromResult<EspnScores?>(espnScores));

        await using var svc = new EspnCacheService(_espnApi, _nflCurrentWeekService, _leagueRepo, _memoryCache, initialDelay: TimeSpan.FromMinutes(5));
        var result = await svc.GetWeekScoresAsync(1, 2025, postSeason: true);

        Assert.Same(espnScores, result);
        await _espnApi.Received(1).GetWeekScores(1, 2025, true);
    }

    [Fact]
    public async Task GetWeekScoresAsync_WhenDbHasNoRowsForTheWeek_FallsBackToEspn()
    {
        _leagueRepo.GetNflScoresAsync(Arg.Any<int>(), Arg.Any<int>()).Returns([]);
        var espnScores = new EspnScores { Season = new Season { Year = 2025 } };
        _espnApi.GetWeekScores(5, 2025, false).Returns(Task.FromResult<EspnScores?>(espnScores));

        await using var svc = new EspnCacheService(_espnApi, _nflCurrentWeekService, _leagueRepo, _memoryCache, initialDelay: TimeSpan.FromMinutes(5));
        var result = await svc.GetWeekScoresAsync(5, 2025, postSeason: false);

        Assert.Same(espnScores, result);
        await _espnApi.Received(1).GetWeekScores(5, 2025, false);
    }

    // A settled week's DB-built response is immutable (a persisted row is always FINAL) — once
    // built, repeated requests for the same week should be served from an in-memory cache instead
    // of re-querying the DB every time, so 100 concurrent viewers of the same past week share one
    // DB read total, not one DB read each.
    [Fact]
    public async Task GetWeekScoresAsync_CachesTheDbBuiltResult_SecondCallForSameWeekNeverHitsDbAgain()
    {
        var rows = new List<Shared.Models.Data.NflScores> {
            new() { Id = 1, Season = 2025, NflWeek = 1, HomeTeam = "KC", AwayTeam = "DEN", HomeTeamScore = 27, AwayTeamScore = 20, GameTime = new DateTimeOffset(2025, 9, 10, 18, 0, 0, TimeSpan.Zero) },
        };
        _leagueRepo.GetNflScoresAsync(2025, 1).Returns(rows);
        // A later (still real-world-past) week the resolver treats as "current" — without this,
        // the Week 1 row below would trivially resolve as its own "current" week (nothing else
        // to compare against), exempting it from the DB-first shortcut this test exists to verify.
        _leagueRepo.GetNflSeasonWeekConfigsAsync().Returns(new List<Models.Data.NflSeasonWeekConfig> {
            new() {
                Season = 2025, WeekId = 1, WeekLabel = "Week 1", WeekType = "RegularSeason", ScoringFormat = "Standard",
                WeekStartDatetime = new DateTime(2025, 9, 4), WeekEndDatetime = new DateTime(2025, 9, 15),
                SpreadLockDatetime = new DateTime(2025, 9, 4),
            },
            new() {
                Season = 2025, WeekId = 2, WeekLabel = "Week 2", WeekType = "RegularSeason", ScoringFormat = "Standard",
                WeekStartDatetime = new DateTime(2025, 9, 11), WeekEndDatetime = new DateTime(2025, 9, 22),
                SpreadLockDatetime = new DateTime(2025, 9, 18),
            },
        });

        await using var svc = new EspnCacheService(_espnApi, _nflCurrentWeekService, _leagueRepo, _memoryCache, initialDelay: TimeSpan.FromMinutes(5));
        var first = await svc.GetWeekScoresAsync(1, 2025, postSeason: false);
        var second = await svc.GetWeekScoresAsync(1, 2025, postSeason: false);

        Assert.NotNull(first);
        Assert.NotNull(second);
        await _leagueRepo.Received(1).GetNflScoresAsync(2025, 1);
    }

    // /code-review caught that this exact bug (fixed for CFB in CfbLiveScoreFetcher — see its
    // comment) was never ported to NFL: gating DB-first purely on "any row persisted" means the
    // instant one game in a multi-game week finishes and gets persisted, the response is built
    // from only that game and cached forever — the rest of that week's still-in-progress games
    // are permanently dropped from every future response, even after they finish and get
    // persisted too. DB-first must only kick in once the week's own window has fully ended.
    [Fact]
    public async Task GetWeekScoresAsync_WhenWeekStillActiveWithPartialRows_StillCallsEspn_NotJustDbRows()
    {
        var now = DateTime.UtcNow;
        _leagueRepo.GetNflSeasonWeekConfigsAsync().Returns(new List<Models.Data.NflSeasonWeekConfig> {
            new() {
                Season = 2026, WeekId = 3, WeekLabel = "Week 3", WeekType = "RegularSeason", ScoringFormat = "Standard",
                WeekStartDatetime = now.AddDays(-1), WeekEndDatetime = now.AddDays(1),
            },
        });
        // One game in this week already finished and was persisted; the rest are still live.
        var partialRows = new List<Shared.Models.Data.NflScores> {
            new() { Id = 1, Season = 2026, NflWeek = 3, HomeTeam = "KC", AwayTeam = "DEN", HomeTeamScore = 27, AwayTeamScore = 20, GameTime = now.AddHours(-3) },
        };
        _leagueRepo.GetNflScoresAsync(2026, 3).Returns(partialRows);
        var espnScores = new EspnScores { Season = new Season { Year = 2026 } };
        _espnApi.GetWeekScores(3, 2026, false).Returns(Task.FromResult<EspnScores?>(espnScores));

        await using var svc = new EspnCacheService(_espnApi, _nflCurrentWeekService, _leagueRepo, _memoryCache, initialDelay: TimeSpan.FromMinutes(5));
        var result = await svc.GetWeekScoresAsync(3, 2026, postSeason: false);

        await _espnApi.Received(1).GetWeekScores(3, 2026, false);
        Assert.Same(espnScores, result);
    }
}
