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
/// frizat-703.6: CFP week=999/date-filter, ranked-team-filter, and legacy date-range branching
/// extracted from CfbScoresJob into a shared fetcher — both the DB-upsert job and CfbCacheService's
/// live-serving path use this same implementation instead of duplicating the logic.
/// </summary>
public class CfbLiveScoreFetcherTests {
    private readonly ICfbApiService _cfbApi = Substitute.For<ICfbApiService>();
    private readonly ICfbRepository _cfbRepo = Substitute.For<ICfbRepository>();
    private readonly ICfbCurrentSlateService _currentSlateService = Substitute.For<ICfbCurrentSlateService>();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _settledCache = new MemoryCache(new MemoryCacheOptions());

    public CfbLiveScoreFetcherTests() {
        // Default: no persisted rows for any slate, so existing tests (which never seed the repo)
        // keep exercising the live-ESPN branch exactly as before this DB-first check was added.
        _cfbRepo.GetScoresForSlateAsync(Arg.Any<int>()).Returns((IEnumerable<CfbScores>)[]);
        // Default: no resolved "current" slate, so existing tests (which never care about the
        // current-slate exemption) keep exercising slateHasEnded exactly as before that check
        // was added.
        _currentSlateService.GetCurrentSlateAsync().Returns((CfbSlateInfo?)null);
        var services = new ServiceCollection();
        services.AddSingleton(_cfbRepo);
        services.AddSingleton(_currentSlateService);
        _scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private CfbLiveScoreFetcher BuildFetcher() => new(_cfbApi, _scopeFactory, _settledCache);

    private static EspnScores BuildScoreboard(
        string eventId = "401677183",
        string homeAbbr = "ORE", string awayAbbr = "OSU",
        TypeName status = TypeName.StatusFinal,
        DateTimeOffset? date = null) {
        var competition = new Competition {
            Date = date ?? new DateTimeOffset(2025, 12, 19, 18, 0, 0, TimeSpan.Zero),
            Competitors = [
                new Competitor { HomeAway = HomeAway.Home, Score = 41, Team = new EspnTeam { Abbreviation = homeAbbr }, Records = [] },
                new Competitor { HomeAway = HomeAway.Away, Score = 21, Team = new EspnTeam { Abbreviation = awayAbbr }, Records = [] },
            ],
            Status = new EspnStatus { Type = new StatusType { Name = status } },
            Odds = [],
        };
        return new EspnScores {
            Season = new Season { Year = 2025, Type = 3 },
            Week = new Week { Number = 1 },
            Events = [new Event { Id = eventId, Season = new Season { Year = 2025, Type = 3 }, Week = new Week { Number = 1 }, Competitions = [competition] }],
        };
    }

    private static EspnScores BuildScoreboardWithRanking(int homeRank = 99, int awayRank = 99) {
        var competition = new Competition {
            Date = new DateTimeOffset(2025, 9, 27, 18, 0, 0, TimeSpan.Zero),
            Competitors = [
                new Competitor { HomeAway = HomeAway.Home, Score = 28, Team = new EspnTeam { Abbreviation = "OSU" }, Records = [], CuratedRank = new CuratedRankInfo { Current = homeRank } },
                new Competitor { HomeAway = HomeAway.Away, Score = 14, Team = new EspnTeam { Abbreviation = "NEB" }, Records = [], CuratedRank = new CuratedRankInfo { Current = awayRank } },
            ],
            Status = new EspnStatus { Type = new StatusType { Name = TypeName.StatusFinal } },
            Odds = [],
        };
        return new EspnScores {
            Season = new Season { Year = 2026, Type = 2 },
            Week = new Week { Number = 5 },
            Events = [new Event { Id = "401999001", Season = new Season { Year = 2026, Type = 2 }, Week = new Week { Number = 5 }, Competitions = [competition] }],
        };
    }

    // ── Regular season / conf-champs: week-based + ranked filter ─────────────

    private static CfbSlates BuildRegularSeasonSlate() => new() {
        Id = 1, Season = 2026, SlateNumber = 5,
        Label = "Week 5", SlateType = "RegularSeason",
        StartDate = new DateOnly(2025, 9, 27), EndDate = new DateOnly(2025, 9, 28),
        EspnWeekNumber = 5, ScoringFormat = "Spread",
    };

    // frizat-9m0: the fetcher no longer filters by rank — every FBS game for the week is returned
    // so the full slate can be persisted for audit. Rank-based visibility now happens downstream,
    // in CfbSpreadJob's IsLeagueEligible computation, not here.
    [Fact]
    public async Task FetchForSlateAsync_RegularSeason_IncludesGame_WhenBothTeamsUnranked() {
        _cfbApi.GetScoresByWeekAsync(5, false).Returns(BuildScoreboardWithRanking(homeRank: 99, awayRank: 99));

        var result = await BuildFetcher().FetchForSlateAsync(BuildRegularSeasonSlate());

        Assert.NotNull(result);
        Assert.Single(result!.Events!);
    }

    [Fact]
    public async Task FetchForSlateAsync_RegularSeason_IncludesGame_WhenOneTeamIsRanked() {
        _cfbApi.GetScoresByWeekAsync(5, false).Returns(BuildScoreboardWithRanking(homeRank: 5, awayRank: 99));

        var result = await BuildFetcher().FetchForSlateAsync(BuildRegularSeasonSlate());

        Assert.NotNull(result);
        Assert.Single(result!.Events!);
    }

    // ── CFP: week=999 bucket + date-window filter ────────────────────────────

    private static CfbSlates BuildCfpSlate() => new() {
        Id = 2, Season = 2026, SlateNumber = 15,
        Label = "CFP First Round", SlateType = "FirstRound",
        StartDate = new DateOnly(2025, 12, 19), EndDate = new DateOnly(2025, 12, 20),
        EspnWeekNumber = 16, ScoringFormat = "NFLDivisional",
    };

    [Fact]
    public async Task FetchForSlateAsync_CfpSlate_UsesCfpGamesAsync_NotWeekQuery() {
        _cfbApi.GetCfpGamesAsync().Returns(BuildScoreboard());

        var result = await BuildFetcher().FetchForSlateAsync(BuildCfpSlate());

        await _cfbApi.Received(1).GetCfpGamesAsync();
        await _cfbApi.DidNotReceive().GetScoresByWeekAsync(Arg.Any<int>(), Arg.Any<bool>());
        Assert.NotNull(result);
        Assert.Single(result!.Events!);
    }

    [Fact]
    public async Task FetchForSlateAsync_CfpSlate_ExcludesGame_OutsideSlateDateRange() {
        var wrongRound = BuildScoreboard(date: new DateTimeOffset(2026, 1, 1, 18, 0, 0, TimeSpan.Zero));
        _cfbApi.GetCfpGamesAsync().Returns(wrongRound);

        var result = await BuildFetcher().FetchForSlateAsync(BuildCfpSlate());

        Assert.Null(result);
    }

    // ── Missing EspnWeekNumber: the control table (CfbSeasonWeekConfig.EspnWeekNumber) is
    // non-nullable and CfbSlateSeederJob is the only producer of CfbSlates rows, so every real
    // slate carries a week number. This app makes week-based ESPN queries only — never date-range
    // "scoreboard" calls. A missing value means the control table wasn't seeded correctly. ────────

    private static CfbSlates BuildSlateMissingWeekNumber() => new() {
        Id = 3, Season = 2025, SlateNumber = 1,
        Label = "Week 1", SlateType = "RegularSeason",
        StartDate = new DateOnly(2025, 12, 19), EndDate = new DateOnly(2025, 12, 20),
        EspnWeekNumber = null,
    };

    [Fact]
    public async Task FetchForSlateAsync_MissingEspnWeekNumber_ReturnsNull_NeverCallsEspn() {
        var result = await BuildFetcher().FetchForSlateAsync(BuildSlateMissingWeekNumber());

        Assert.Null(result);
        await _cfbApi.DidNotReceiveWithAnyArgs().GetScoresByWeekAsync(default, default);
        await _cfbApi.DidNotReceive().GetCfpGamesAsync();
    }

    // /code-review caught the DB-first branch silently defaulting a missing EspnWeekNumber to
    // week 0 instead of applying the same guard the ESPN-fallback path already used — even with
    // persisted rows present, a slate with no week number is a data-integrity problem, not
    // something to paper over.
    [Fact]
    public async Task FetchForSlateAsync_MissingEspnWeekNumber_ReturnsNull_EvenWithPersistedRows() {
        var slate = BuildSlateMissingWeekNumber(); // EndDate 2025-12-20 — already ended
        var rows = new List<CfbScores> {
            new() { Id = 1, CfbSlateId = slate.Id, HomeTeam = "OSU", AwayTeam = "NEB", HomeTeamScore = 28, AwayTeamScore = 14, GameStatus = TypeName.StatusFinal, GameTime = new DateTimeOffset(2025, 12, 19, 18, 0, 0, TimeSpan.Zero) },
        };
        _cfbRepo.GetScoresForSlateAsync(slate.Id).Returns((IEnumerable<CfbScores>)rows);

        var result = await BuildFetcher().FetchForSlateAsync(slate);

        Assert.Null(result);
        await _cfbApi.DidNotReceiveWithAnyArgs().GetScoresByWeekAsync(default, default);
        await _cfbApi.DidNotReceive().GetCfpGamesAsync();
    }

    // ── DB-first: a slate whose games are already persisted (always FINAL — CfbScoresJob only
    // writes finished games) is served from CfbScores instead of a live ESPN call, so 100
    // concurrent viewers of the same past slate share one DB read instead of each hitting ESPN. ──

    [Fact]
    public async Task FetchForSlateAsync_WhenDbHasPersistedRowsForTheSlate_ReturnsDbBuiltScores_NeverCallsEspn() {
        var slate = BuildRegularSeasonSlate();
        var rows = new List<CfbScores> {
            new() { Id = 1, CfbSlateId = slate.Id, HomeTeam = "OSU", AwayTeam = "NEB", HomeTeamScore = 28, AwayTeamScore = 14, GameStatus = TypeName.StatusFinal, GameTime = new DateTimeOffset(2025, 9, 27, 18, 0, 0, TimeSpan.Zero) },
        };
        _cfbRepo.GetScoresForSlateAsync(slate.Id).Returns((IEnumerable<CfbScores>)rows);

        var result = await BuildFetcher().FetchForSlateAsync(slate);

        Assert.NotNull(result);
        var comp = result!.Events!.Single().Competitions[0];
        var home = comp.Competitors.Single(c => c.HomeAway == HomeAway.Home);
        Assert.Equal("OSU", home.Team.Abbreviation);
        Assert.Equal(28, home.Score);
        Assert.Equal(TypeName.StatusFinal, comp.Status.Type.Name);
        await _cfbApi.DidNotReceiveWithAnyArgs().GetScoresByWeekAsync(default, default);
        await _cfbApi.DidNotReceive().GetCfpGamesAsync();
    }

    // frizat: cfbAdapter.ts's current-slate path now always calls this fetcher for whichever
    // slate the control table (ICfbCurrentSlateService) resolves as current — that slate must
    // always be live-fetched, even if its own calendar window looks "ended" and persisted rows
    // exist, or the demo's frozen in-progress fixture data never surfaces for it.
    [Fact]
    public async Task FetchForSlateAsync_WhenSlateIsTheResolvedCurrentSlate_AlwaysCallsEspn_EvenWithPersistedRowsAndEndedWindow() {
        var slate = BuildRegularSeasonSlate();
        var rows = new List<CfbScores> {
            new() { Id = 1, CfbSlateId = slate.Id, HomeTeam = "OSU", AwayTeam = "NEB", HomeTeamScore = 28, AwayTeamScore = 14, GameStatus = TypeName.StatusFinal, GameTime = new DateTimeOffset(2025, 9, 27, 18, 0, 0, TimeSpan.Zero) },
        };
        _cfbRepo.GetScoresForSlateAsync(slate.Id).Returns((IEnumerable<CfbScores>)rows);
        _currentSlateService.GetCurrentSlateAsync().Returns(new CfbSlateInfo(
            slate.Id, slate.Season, slate.SlateNumber, slate.Label, slate.SlateType,
            slate.StartDate, slate.EndDate, null, DateTime.UtcNow));
        _cfbApi.GetScoresByWeekAsync(5, false).Returns(BuildScoreboardWithRanking());

        await BuildFetcher().FetchForSlateAsync(slate);

        await _cfbApi.Received(1).GetScoresByWeekAsync(5, false);
    }

    [Fact]
    public async Task FetchForSlateAsync_WhenDbHasNoRowsForTheSlate_FallsBackToEspn() {
        var slate = BuildRegularSeasonSlate();
        _cfbRepo.GetScoresForSlateAsync(slate.Id).Returns((IEnumerable<CfbScores>)[]);
        _cfbApi.GetScoresByWeekAsync(5, false).Returns(BuildScoreboardWithRanking());

        var result = await BuildFetcher().FetchForSlateAsync(slate);

        Assert.NotNull(result);
        await _cfbApi.Received(1).GetScoresByWeekAsync(5, false);
    }

    // /code-review caught a real bug: gating DB-first purely on "rows.Count > 0" means the moment
    // ONE game in a multi-game slate finishes and gets persisted, every subsequent call for that
    // slate permanently stops calling ESPN — the rest of the slate's still-in-progress games are
    // never discovered or persisted. This backs BOTH CfbScoresJob (which would then never finish
    // collecting that slate's scores) AND CfbCacheService's live poll of the CURRENT slate (which
    // would freeze the Scores page for every other game in progress). DB-first must only kick in
    // once the slate's own window has fully ended — while a slate is still active, ESPN is always
    // the source of truth regardless of how many of its games have already been persisted.
    [Fact]
    public async Task FetchForSlateAsync_WhenSlateStillActiveWithPartialRows_StillCallsEspn_NotJustDbRows() {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var activeSlate = new CfbSlates {
            Id = 9, Season = 2026, SlateNumber = 3, Label = "Week 3", SlateType = "RegularSeason",
            StartDate = today.AddDays(-1), EndDate = today.AddDays(1), EspnWeekNumber = 3, ScoringFormat = "Spread",
        };
        // One game in this slate already finished and was persisted; the rest are still live.
        var partialRows = new List<CfbScores> {
            new() { Id = 1, CfbSlateId = activeSlate.Id, HomeTeam = "OSU", AwayTeam = "NEB", HomeTeamScore = 28, AwayTeamScore = 14, GameStatus = TypeName.StatusFinal, GameTime = DateTimeOffset.UtcNow.AddHours(-3) },
        };
        _cfbRepo.GetScoresForSlateAsync(activeSlate.Id).Returns((IEnumerable<CfbScores>)partialRows);
        _cfbApi.GetScoresByWeekAsync(3, false).Returns(BuildScoreboardWithRanking());

        var result = await BuildFetcher().FetchForSlateAsync(activeSlate);

        await _cfbApi.Received(1).GetScoresByWeekAsync(3, false);
        Assert.NotNull(result);
        Assert.Single(result!.Events!);
    }

    // A settled slate's DB-built response is immutable (a persisted row is always FINAL) — once
    // built, repeated requests for the same slate should be served from an in-memory cache instead
    // of re-querying the DB every time, so concurrent viewers of the same settled slate share one
    // DB read total, not one DB read each.
    [Fact]
    public async Task FetchForSlateAsync_CachesTheDbBuiltResult_SecondCallForSameSlateNeverHitsDbAgain() {
        var slate = BuildRegularSeasonSlate(); // EndDate 2025-9-28 — already ended
        var rows = new List<CfbScores> {
            new() { Id = 1, CfbSlateId = slate.Id, HomeTeam = "OSU", AwayTeam = "NEB", HomeTeamScore = 28, AwayTeamScore = 14, GameStatus = TypeName.StatusFinal, GameTime = new DateTimeOffset(2025, 9, 27, 18, 0, 0, TimeSpan.Zero) },
        };
        _cfbRepo.GetScoresForSlateAsync(slate.Id).Returns((IEnumerable<CfbScores>)rows);

        var fetcher = BuildFetcher();
        var first = await fetcher.FetchForSlateAsync(slate);
        var second = await fetcher.FetchForSlateAsync(slate);

        Assert.NotNull(first);
        Assert.NotNull(second);
        await _cfbRepo.Received(1).GetScoresForSlateAsync(slate.Id);
    }
}
