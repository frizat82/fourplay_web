using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Enum;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// Mirrors EspnCacheServiceTests exactly (frizat-703.6 unification, frizat-d0t settled-cache
/// unification) — same PeriodicRefreshCache engine, same deterministic-wait pattern, only the
/// fetch delegate (current slate + CFP/ranked filtering) differs. Uses a real
/// IServiceScopeFactory (backed by a minimal ServiceCollection) since CfbCacheService creates a
/// DI scope per refresh to safely consume the Scoped ICfbCurrentSlateService/ICfbRepository from
/// a Singleton — see CfbCacheService's own comment.
/// </summary>
public class CfbCacheServiceTests
{
    private readonly ICfbCurrentSlateService _currentSlateService;
    private readonly ICfbRepository _cfbRepo;
    private readonly ICfbLiveScoreFetcher _fetcher;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _memoryCache = new MemoryCache(new MemoryCacheOptions());

    // StartDate/EndDate relative to "now" (not a fixed calendar date) so this slate is always
    // "currently active" for the SeasonWindowResolver-based gate CfbCacheService now checks
    // before fetching — tests below shouldn't have to also fight an unrelated off-season skip.
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private static readonly CfbSlateInfo DefaultSlateInfo = new(
        Id: 1, Season: 2026, SlateNumber: 5, Label: "Week 5", SlateType: "RegularSeason",
        StartDate: Today.AddDays(-1), EndDate: Today.AddDays(1), FirstGameUtc: null,
        SpreadLockDatetime: DateTime.UtcNow.AddDays(-7));

    private static readonly CfbSlates DefaultSlate = new() {
        Id = 1, Season = 2026, SlateNumber = 5, Label = "Week 5", SlateType = "RegularSeason",
        StartDate = Today.AddDays(-1), EndDate = Today.AddDays(1),
        EspnWeekNumber = 5, ScoringFormat = "Spread",
    };

    // A separate, already-ENDED slate (fixed past calendar dates) for the settled-cache tests
    // below — DefaultSlate is deliberately always-active so it can't exercise the DB-first path.
    private static readonly CfbSlates SettledSlate = new() {
        Id = 2, Season = 2025, SlateNumber = 4, Label = "Week 4", SlateType = "RegularSeason",
        StartDate = new DateOnly(2025, 9, 27), EndDate = new DateOnly(2025, 9, 28),
        EspnWeekNumber = 4, ScoringFormat = "Spread",
    };

    public CfbCacheServiceTests()
    {
        _currentSlateService = Substitute.For<ICfbCurrentSlateService>();
        _cfbRepo = Substitute.For<ICfbRepository>();
        _fetcher = Substitute.For<ICfbLiveScoreFetcher>();
        _currentSlateService.GetCurrentSlateAsync().Returns(DefaultSlateInfo);
        _cfbRepo.GetSlateByIdAsync(DefaultSlate.Id).Returns(DefaultSlate);
        _cfbRepo.GetSlateByIdAsync(SettledSlate.Id).Returns(SettledSlate);
        // Default: no persisted rows for any slate, so existing tests (which never seed the
        // repo) keep exercising the live-fetch branch exactly as before GetSlateScoresAsync
        // existed.
        _cfbRepo.GetScoresForSlateAsync(Arg.Any<int>()).Returns((IEnumerable<CfbScores>)[]);
        // Default: a season is active, so existing tests keep exercising the live-fetch branch
        // unchanged by the new off-season gate.
        _currentSlateService.IsSeasonActiveAsync().Returns(true);

        var services = new ServiceCollection();
        services.AddSingleton(_currentSlateService);
        services.AddSingleton(_cfbRepo);
        _scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private CfbCacheService BuildService(TimeSpan? initialDelay = null) =>
        new(_scopeFactory, _fetcher, _memoryCache, initialDelay);

    private static async Task WaitForScoresChangedAsync(CfbCacheService svc, TimeSpan? timeout = null)
    {
        var tcs = new TaskCompletionSource();
        svc.ScoresChanged += () => tcs.TrySetResult();
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(timeout ?? TimeSpan.FromSeconds(5)));
        Assert.True(completed == tcs.Task, "Timed out waiting for ScoresChanged to fire.");
    }

    private static EspnScores BuildScoreboard(TypeName status = TypeName.StatusInProgress, int homeScore = 14, int awayScore = 7) =>
        new() {
            Events = [new Event { Id = "1", Competitions = [new Competition {
                Status = new EspnStatus { Type = new StatusType { Name = status, Description = status switch {
                    TypeName.StatusFinal => Description.Final,
                    TypeName.StatusHalftime => Description.Halftime,
                    TypeName.StatusInProgress => Description.InProgress,
                    TypeName.StatusScheduled => Description.Scheduled,
                    _ => Description.EndOfPeriod,
                } } },
                Competitors = [
                    new Competitor { HomeAway = HomeAway.Home, Score = homeScore, Team = new EspnTeam { Abbreviation = "IND" }, Records = [] },
                    new Competitor { HomeAway = HomeAway.Away, Score = awayScore, Team = new EspnTeam { Abbreviation = "ATL" }, Records = [] },
                ],
                Odds = [],
            }] }],
        };

    [Fact]
    public async Task GetScoresAsync_WhenNoCurrentSlate_ReturnsNull()
    {
        _currentSlateService.GetCurrentSlateAsync().Returns((CfbSlateInfo?)null);

        await using var svc = BuildService(TimeSpan.FromMinutes(5));
        await Task.Delay(300);

        Assert.Null(await svc.GetScoresAsync());
        await _fetcher.DidNotReceive().FetchForSlateAsync(Arg.Any<CfbSlates>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task GetScoresAsync_ResolvesFullSlateEntity_ThenFetches()
    {
        _fetcher.FetchForSlateAsync(Arg.Is<CfbSlates>(s => s.Id == DefaultSlate.Id), Arg.Any<bool>()).Returns(BuildScoreboard());

        await using var svc = BuildService(initialDelay: TimeSpan.FromMilliseconds(50));
        await WaitForScoresChangedAsync(svc);

        var result = await svc.GetScoresAsync();
        Assert.NotNull(result);
        Assert.Single(result!.Events!);
    }

    [Fact]
    public async Task ScoresChanged_Fires_WhenLiveScoreChanges()
    {
        int fireCount = 0;
        _fetcher.FetchForSlateAsync(Arg.Any<CfbSlates>(), Arg.Any<bool>()).Returns(BuildScoreboard(homeScore: 14, awayScore: 7));

        await using var svc = BuildService(initialDelay: TimeSpan.FromMilliseconds(50));
        svc.ScoresChanged += () => Interlocked.Increment(ref fireCount);

        await WaitForScoresChangedAsync(svc);

        Assert.Equal(1, fireCount);
    }

    // Off-season gating — the poller must not hit ESPN when no season is active (frizat plan:
    // wobbly-chasing-lynx). Previously CfbCurrentSlateService's ConfiguredSeason hardcode meant
    // GetCurrentSlateAsync's null-ness happened to gate this correctly by accident; now that it
    // always resolves *something*, IsSeasonActive is the purpose-built check instead.
    [Fact]
    public async Task GetScoresAsync_NeverFetches_WhenNoSeasonIsCurrentlyActive()
    {
        _currentSlateService.IsSeasonActiveAsync().Returns(false);

        await using var svc = BuildService(initialDelay: TimeSpan.FromMilliseconds(50));
        await Task.Delay(300);

        Assert.Null(await svc.GetScoresAsync());
        await _fetcher.DidNotReceive().FetchForSlateAsync(Arg.Any<CfbSlates>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task GetScoresAsync_WhenFetcherReturnsNull_ReturnsNull()
    {
        _fetcher.FetchForSlateAsync(Arg.Any<CfbSlates>(), Arg.Any<bool>()).Returns((EspnScores?)null);

        await using var svc = BuildService(TimeSpan.FromMinutes(5));
        await Task.Delay(300);

        Assert.Null(await svc.GetScoresAsync());
    }

    // ── GetSlateScoresAsync — settled-slate DB-first cache (migrated from
    // CfbLiveScoreFetcherTests.cs, frizat-d0t) ──────────────────────────────────────────────

    [Fact]
    public async Task GetSlateScoresAsync_WhenDbHasPersistedRowsForTheSlate_ReturnsDbBuiltScores_NeverCallsFetcher()
    {
        var rows = new List<CfbScores> {
            new() { Id = 1, CfbSlateId = SettledSlate.Id, HomeTeam = "OSU", AwayTeam = "NEB", HomeTeamScore = 28, AwayTeamScore = 14, GameStatus = TypeName.StatusFinal, GameTime = new DateTimeOffset(2025, 9, 27, 18, 0, 0, TimeSpan.Zero) },
        };
        _cfbRepo.GetScoresForSlateAsync(SettledSlate.Id).Returns((IEnumerable<CfbScores>)rows);

        await using var svc = BuildService(TimeSpan.FromMinutes(5));
        var result = await svc.GetSlateScoresAsync(SettledSlate.Id);

        Assert.NotNull(result);
        var comp = result!.Events!.Single().Competitions[0];
        var home = comp.Competitors.Single(c => c.HomeAway == HomeAway.Home);
        Assert.Equal("OSU", home.Team.Abbreviation);
        Assert.Equal(28, home.Score);
        Assert.Equal(TypeName.StatusFinal, comp.Status.Type.Name);
        await _fetcher.DidNotReceive().FetchForSlateAsync(Arg.Any<CfbSlates>(), Arg.Any<bool>());
    }

    // frizat: cfbAdapter.ts's current-slate path now always calls this endpoint for whichever
    // slate the control table (ICfbCurrentSlateService) resolves as current — that slate must
    // always be live-fetched, even if its own calendar window looks "ended" and persisted rows
    // exist, or the demo's frozen in-progress fixture data never surfaces for it.
    [Fact]
    public async Task GetSlateScoresAsync_WhenSlateIsTheResolvedCurrentSlate_AlwaysCallsFetcher_EvenWithPersistedRowsAndEndedWindow()
    {
        var rows = new List<CfbScores> {
            new() { Id = 1, CfbSlateId = SettledSlate.Id, HomeTeam = "OSU", AwayTeam = "NEB", HomeTeamScore = 28, AwayTeamScore = 14, GameStatus = TypeName.StatusFinal, GameTime = new DateTimeOffset(2025, 9, 27, 18, 0, 0, TimeSpan.Zero) },
        };
        _cfbRepo.GetScoresForSlateAsync(SettledSlate.Id).Returns((IEnumerable<CfbScores>)rows);
        _currentSlateService.GetCurrentSlateAsync().Returns(new CfbSlateInfo(
            SettledSlate.Id, SettledSlate.Season, SettledSlate.SlateNumber, SettledSlate.Label, SettledSlate.SlateType,
            SettledSlate.StartDate, SettledSlate.EndDate, null, DateTime.UtcNow));
        var espnScores = BuildScoreboard();
        _fetcher.FetchForSlateAsync(Arg.Is<CfbSlates>(s => s.Id == SettledSlate.Id), Arg.Any<bool>()).Returns(espnScores);

        await using var svc = BuildService(TimeSpan.FromMinutes(5));
        var result = await svc.GetSlateScoresAsync(SettledSlate.Id);

        Assert.Same(espnScores, result);
        // isCurrentSlate must be the already-resolved true, not re-derived by the fetcher (which
        // no longer has the means to — it takes this as a parameter now, frizat-d0t).
        await _fetcher.Received(1).FetchForSlateAsync(Arg.Is<CfbSlates>(s => s.Id == SettledSlate.Id), isCurrentSlate: true);
    }

    [Fact]
    public async Task GetSlateScoresAsync_WhenDbHasNoRowsForTheSlate_FallsBackToFetcher()
    {
        var espnScores = BuildScoreboard();
        _fetcher.FetchForSlateAsync(Arg.Is<CfbSlates>(s => s.Id == DefaultSlate.Id), Arg.Any<bool>()).Returns(espnScores);

        await using var svc = BuildService(TimeSpan.FromMinutes(5));
        var result = await svc.GetSlateScoresAsync(DefaultSlate.Id);

        Assert.Same(espnScores, result);
        // DefaultSlate.Id matches the constructor's default GetCurrentSlateAsync() setup — true.
        await _fetcher.Received(1).FetchForSlateAsync(Arg.Is<CfbSlates>(s => s.Id == DefaultSlate.Id), isCurrentSlate: true);
    }

    [Fact]
    public async Task GetSlateScoresAsync_WhenNoSlateMatchesTheRequestedId_ReturnsNull_NeverCallsFetcher()
    {
        _cfbRepo.GetSlateByIdAsync(999).Returns((CfbSlates?)null);

        await using var svc = BuildService(TimeSpan.FromMinutes(5));
        var result = await svc.GetSlateScoresAsync(999);

        Assert.Null(result);
        await _fetcher.DidNotReceiveWithAnyArgs().FetchForSlateAsync(default!, default!);
    }

    // /code-review caught the DB-first branch silently defaulting a missing EspnWeekNumber to
    // week 0 instead of applying the same guard the ESPN-fallback path already used — even with
    // persisted rows present, a slate with no week number is a data-integrity problem, not
    // something to paper over.
    [Fact]
    public async Task GetSlateScoresAsync_MissingEspnWeekNumber_ReturnsNull_EvenWithPersistedRows()
    {
        var slate = new CfbSlates {
            Id = 3, Season = 2025, SlateNumber = 1, Label = "Week 1", SlateType = "RegularSeason",
            StartDate = new DateOnly(2025, 12, 19), EndDate = new DateOnly(2025, 12, 20), EspnWeekNumber = null,
        };
        _cfbRepo.GetSlateByIdAsync(slate.Id).Returns(slate);
        var rows = new List<CfbScores> {
            new() { Id = 1, CfbSlateId = slate.Id, HomeTeam = "OSU", AwayTeam = "NEB", HomeTeamScore = 28, AwayTeamScore = 14, GameStatus = TypeName.StatusFinal, GameTime = new DateTimeOffset(2025, 12, 19, 18, 0, 0, TimeSpan.Zero) },
        };
        _cfbRepo.GetScoresForSlateAsync(slate.Id).Returns((IEnumerable<CfbScores>)rows);

        await using var svc = BuildService(TimeSpan.FromMinutes(5));
        var result = await svc.GetSlateScoresAsync(slate.Id);

        Assert.Null(result);
        await _fetcher.DidNotReceiveWithAnyArgs().FetchForSlateAsync(default!, default!);
    }

    // A settled slate's DB-built response is immutable (a persisted row is always FINAL) — once
    // built, repeated requests for the same slate should be served from an in-memory cache instead
    // of re-querying the DB every time, so concurrent viewers of the same settled slate share one
    // DB read total, not one DB read each.
    [Fact]
    public async Task GetSlateScoresAsync_CachesTheDbBuiltResult_SecondCallForSameSlateNeverHitsDbAgain()
    {
        var rows = new List<CfbScores> {
            new() { Id = 1, CfbSlateId = SettledSlate.Id, HomeTeam = "OSU", AwayTeam = "NEB", HomeTeamScore = 28, AwayTeamScore = 14, GameStatus = TypeName.StatusFinal, GameTime = new DateTimeOffset(2025, 9, 27, 18, 0, 0, TimeSpan.Zero) },
        };
        _cfbRepo.GetScoresForSlateAsync(SettledSlate.Id).Returns((IEnumerable<CfbScores>)rows);

        await using var svc = BuildService(TimeSpan.FromMinutes(5));
        var first = await svc.GetSlateScoresAsync(SettledSlate.Id);
        _cfbRepo.ClearReceivedCalls();
        var second = await svc.GetSlateScoresAsync(SettledSlate.Id);

        Assert.NotNull(first);
        Assert.NotNull(second);
        // The cache-hit fast path skips the slate/current-slate DB resolution entirely on the
        // second call, not just the settled-rows read — GetSlateByIdAsync is never called again.
        await _cfbRepo.DidNotReceiveWithAnyArgs().GetSlateByIdAsync(default);
        await _cfbRepo.DidNotReceiveWithAnyArgs().GetScoresForSlateAsync(default);
    }

    [Fact]
    public async Task InvalidateSlateCache_ForcesTheNextRequestToRebuildFromTheDb()
    {
        var rows = new List<CfbScores> {
            new() { Id = 1, CfbSlateId = SettledSlate.Id, HomeTeam = "OSU", AwayTeam = "NEB", HomeTeamScore = 28, AwayTeamScore = 14, GameStatus = TypeName.StatusFinal, GameTime = new DateTimeOffset(2025, 9, 27, 18, 0, 0, TimeSpan.Zero) },
        };
        _cfbRepo.GetScoresForSlateAsync(SettledSlate.Id).Returns((IEnumerable<CfbScores>)rows);

        await using var svc = BuildService(TimeSpan.FromMinutes(5));
        await svc.GetSlateScoresAsync(SettledSlate.Id);
        svc.InvalidateSlateCache(SettledSlate.Id);
        await svc.GetSlateScoresAsync(SettledSlate.Id);

        await _cfbRepo.Received(2).GetScoresForSlateAsync(SettledSlate.Id);
    }
}
