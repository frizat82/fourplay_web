using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Enum;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// Mirrors EspnCacheServiceTests exactly (frizat-703.6 unification) — same PeriodicRefreshCache
/// engine, same deterministic-wait pattern, only the fetch delegate (current slate + CFP/ranked
/// filtering) differs. Uses a real IServiceScopeFactory (backed by a minimal ServiceCollection)
/// since CfbCacheService creates a DI scope per refresh to safely consume the Scoped
/// ICfbCurrentSlateService/ICfbRepository from a Singleton — see CfbCacheService's own comment.
/// </summary>
public class CfbCacheServiceTests
{
    private readonly ICfbCurrentSlateService _currentSlateService;
    private readonly ICfbRepository _cfbRepo;
    private readonly ICfbLiveScoreFetcher _fetcher;
    private readonly IServiceScopeFactory _scopeFactory;

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

    public CfbCacheServiceTests()
    {
        _currentSlateService = Substitute.For<ICfbCurrentSlateService>();
        _cfbRepo = Substitute.For<ICfbRepository>();
        _fetcher = Substitute.For<ICfbLiveScoreFetcher>();
        _currentSlateService.GetCurrentSlateAsync().Returns(DefaultSlateInfo);
        _cfbRepo.GetSlateByIdAsync(DefaultSlate.Id).Returns(DefaultSlate);
        // Default: a season is active, so existing tests keep exercising the live-fetch branch
        // unchanged by the new off-season gate.
        _currentSlateService.IsSeasonActiveAsync().Returns(true);

        var services = new ServiceCollection();
        services.AddSingleton(_currentSlateService);
        services.AddSingleton(_cfbRepo);
        _scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

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

        await using var svc = new CfbCacheService(_scopeFactory, _fetcher);
        await Task.Delay(300);

        Assert.Null(await svc.GetScoresAsync());
        await _fetcher.DidNotReceive().FetchForSlateAsync(Arg.Any<CfbSlates>());
    }

    [Fact]
    public async Task GetScoresAsync_ResolvesFullSlateEntity_ThenFetches()
    {
        _fetcher.FetchForSlateAsync(Arg.Is<CfbSlates>(s => s.Id == DefaultSlate.Id)).Returns(BuildScoreboard());

        await using var svc = new CfbCacheService(_scopeFactory, _fetcher, initialDelay: TimeSpan.FromMilliseconds(50));
        await WaitForScoresChangedAsync(svc);

        var result = await svc.GetScoresAsync();
        Assert.NotNull(result);
        Assert.Single(result!.Events!);
    }

    [Fact]
    public async Task ScoresChanged_Fires_WhenLiveScoreChanges()
    {
        int fireCount = 0;
        _fetcher.FetchForSlateAsync(Arg.Any<CfbSlates>()).Returns(BuildScoreboard(homeScore: 14, awayScore: 7));

        await using var svc = new CfbCacheService(_scopeFactory, _fetcher, initialDelay: TimeSpan.FromMilliseconds(50));
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

        await using var svc = new CfbCacheService(_scopeFactory, _fetcher, initialDelay: TimeSpan.FromMilliseconds(50));
        await Task.Delay(300);

        Assert.Null(await svc.GetScoresAsync());
        await _fetcher.DidNotReceive().FetchForSlateAsync(Arg.Any<CfbSlates>());
    }

    [Fact]
    public async Task GetScoresAsync_WhenFetcherReturnsNull_ReturnsNull()
    {
        _fetcher.FetchForSlateAsync(Arg.Any<CfbSlates>()).Returns((EspnScores?)null);

        await using var svc = new CfbCacheService(_scopeFactory, _fetcher);
        await Task.Delay(300);

        Assert.Null(await svc.GetScoresAsync());
    }
}
