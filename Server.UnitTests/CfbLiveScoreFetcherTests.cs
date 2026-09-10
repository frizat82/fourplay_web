using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Enum;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// frizat-703.6: CFP week=999/date-filter, ranked-team-filter, and legacy date-range branching
/// extracted from CfbScoresJob into a shared fetcher. frizat-d0t: this fetcher is now a PURE
/// fetch, no caching — the settled-slate DB-reconstruction + cache that used to live here (with
/// its own current-slate exemption and window-ended gating) moved to
/// CfbCacheService.GetSlateScoresAsync (see CfbCacheServiceTests.cs for that coverage), mirroring
/// where NFL's equivalent already lived (EspnCacheService.GetWeekScoresAsync). This file now only
/// covers the pure ESPN-call shape — none of it exercises the replay-mode snapshot merge (that
/// needs a registered ReplayCacheService, which none of these tests set up), so every call below
/// passes isCurrentSlate: false.
/// </summary>
public class CfbLiveScoreFetcherTests {
    private readonly ICfbApiService _cfbApi = Substitute.For<ICfbApiService>();
    private readonly IServiceProvider _serviceProvider = new ServiceCollection().BuildServiceProvider();

    private CfbLiveScoreFetcher BuildFetcher() => new(_cfbApi, _serviceProvider);

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
            Status = new EspnStatus { Type = new StatusType { Name = status, Description = status switch {
                TypeName.StatusFinal => Description.Final,
                TypeName.StatusHalftime => Description.Halftime,
                TypeName.StatusInProgress => Description.InProgress,
                TypeName.StatusScheduled => Description.Scheduled,
                _ => Description.EndOfPeriod,
            } } },
            Odds = [],
        };
        return new EspnScores {
            Season = new Season { Year = 2025, Type = 3 },
            Week = new Week { Number = 1 },
            Events = [new Event { Id = eventId, Season = new Season { Year = 2025, Type = 3 }, Week = new Week { Number = 1 }, Competitions = [competition] }],
        };
    }

    private static EspnScores BuildScoreboardWithRanking(int homeRank = 99, int awayRank = 99, DateTimeOffset? date = null) {
        var competition = new Competition {
            Date = date ?? new DateTimeOffset(2025, 9, 27, 18, 0, 0, TimeSpan.Zero),
            Competitors = [
                new Competitor { HomeAway = HomeAway.Home, Score = 28, Team = new EspnTeam { Abbreviation = "OSU" }, Records = [], CuratedRank = new CuratedRankInfo { Current = homeRank } },
                new Competitor { HomeAway = HomeAway.Away, Score = 14, Team = new EspnTeam { Abbreviation = "NEB" }, Records = [], CuratedRank = new CuratedRankInfo { Current = awayRank } },
            ],
            Status = new EspnStatus { Type = new StatusType { Name = TypeName.StatusFinal, Description = Description.Final } },
            Odds = [],
        };
        return new EspnScores {
            Season = new Season { Year = 2026, Type = 2 },
            Week = new Week { Number = 5 },
            Events = [new Event { Id = "401999001", Season = new Season { Year = 2026, Type = 2 }, Week = new Week { Number = 5 }, Competitions = [competition] }],
        };
    }

    // ── Regular season / conf-champs: control-table date-range query (frizat-11t) ────────────

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
        var slate = BuildRegularSeasonSlate();
        _cfbApi.GetScoresByDateRangeAsync(slate.StartDate, slate.EndDate).Returns(BuildScoreboardWithRanking(homeRank: 99, awayRank: 99));

        var result = await BuildFetcher().FetchForSlateAsync(slate, isCurrentSlate: false);

        Assert.NotNull(result);
        Assert.Single(result!.Events!);
    }

    [Fact]
    public async Task FetchForSlateAsync_RegularSeason_IncludesGame_WhenOneTeamIsRanked() {
        var slate = BuildRegularSeasonSlate();
        _cfbApi.GetScoresByDateRangeAsync(slate.StartDate, slate.EndDate).Returns(BuildScoreboardWithRanking(homeRank: 5, awayRank: 99));

        var result = await BuildFetcher().FetchForSlateAsync(slate, isCurrentSlate: false);

        Assert.NotNull(result);
        Assert.Single(result!.Events!);
    }

    // frizat-11t: live incident regression. ESPN's own week=N bucket doesn't respect our slate's
    // date window — a team with an early "week 0"-ish opener plus its real week-N game can have BOTH
    // land in one week=N fetch (real example: USC's Aug 29 SJSU game and Sep 5 Fresno State game both
    // came back under ESPN's week=1). The frontend's live-score join (cfbAdapter.ts's
    // buildGamesFromEspn, keyed by home-team abbreviation only, deliberately NOT changed by this fix)
    // assumes one event per team per slate; a second stray event for the same team silently clobbers
    // the correct one. Querying by our own control-table date range instead of ESPN's week number
    // means the fetch itself only ever returns events ESPN placed within OUR slate's actual window —
    // "our week wins, not ESPN's" — so that assumption holds. This test proves the call is built from
    // slate.StartDate/EndDate, not derived from slate.EspnWeekNumber in any way.
    [Fact]
    public async Task FetchForSlateAsync_RegularSeason_QueriesByControlTableDateRange_NotEspnWeekNumber() {
        // EspnWeekNumber is deliberately something a week-based call would never coincidentally
        // match against these StartDate/EndDate values — proves the fetch is date-driven, not
        // secretly still week-driven.
        var slate = new CfbSlates {
            Id = 19, Season = 2026, SlateNumber = 1, Label = "Week 1", SlateType = "RegularSeason",
            StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 9, 7),
            EspnWeekNumber = 1, ScoringFormat = "Standard",
        };
        _cfbApi.GetScoresByDateRangeAsync(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 7))
            .Returns(BuildScoreboardWithRanking(date: new DateTimeOffset(2026, 9, 5, 1, 0, 0, TimeSpan.Zero)));

        var result = await BuildFetcher().FetchForSlateAsync(slate, isCurrentSlate: false);

        await _cfbApi.Received(1).GetScoresByDateRangeAsync(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 7));
        await _cfbApi.DidNotReceiveWithAnyArgs().GetCfpGamesAsync();
        Assert.NotNull(result);
    }

    // /code-review: defense-in-depth. The query-time dates= param is verified (live) to exclude
    // out-of-window events, but this fix's whole premise is "don't fully trust ESPN's own
    // bucketing" — so apply the same downstream date-window filter FetchCfpAsync already uses,
    // rather than relying solely on ESPN correctly honoring the query param in every case (e.g. a
    // timezone-boundary edge case on a late-night/West-Coast CFB kickoff).
    [Fact]
    public async Task FetchForSlateAsync_RegularSeason_ExcludesGame_OutsideSlateDateRange_EvenIfEspnReturnsIt() {
        var slate = new CfbSlates {
            Id = 19, Season = 2026, SlateNumber = 1, Label = "Week 1", SlateType = "RegularSeason",
            StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 9, 7),
            EspnWeekNumber = 1, ScoringFormat = "Standard",
        };
        var inWindow = new Competition {
            Date = new DateTimeOffset(2026, 9, 5, 1, 0, 0, TimeSpan.Zero),
            Competitors = [
                new Competitor { HomeAway = HomeAway.Home, Score = 0, Team = new EspnTeam { Abbreviation = "USC" }, Records = [] },
                new Competitor { HomeAway = HomeAway.Away, Score = 0, Team = new EspnTeam { Abbreviation = "FRES" }, Records = [] },
            ],
            Status = new EspnStatus { Type = new StatusType { Name = TypeName.StatusScheduled, Description = Description.Scheduled } },
            Odds = [],
        };
        var outOfWindow = new Competition {
            Date = new DateTimeOffset(2026, 8, 29, 19, 0, 0, TimeSpan.Zero),
            Competitors = [
                new Competitor { HomeAway = HomeAway.Home, Score = 42, Team = new EspnTeam { Abbreviation = "USC" }, Records = [] },
                new Competitor { HomeAway = HomeAway.Away, Score = 26, Team = new EspnTeam { Abbreviation = "SJSU" }, Records = [] },
            ],
            Status = new EspnStatus { Type = new StatusType { Name = TypeName.StatusFinal, Description = Description.Final } },
            Odds = [],
        };
        var scoreboard = new EspnScores {
            Season = new Season { Year = 2026, Type = 2 },
            Week = new Week { Number = 1 },
            Events = [
                new Event { Id = "1", Season = new Season { Year = 2026, Type = 2 }, Week = new Week { Number = 1 }, Competitions = [inWindow] },
                new Event { Id = "2", Season = new Season { Year = 2026, Type = 2 }, Week = new Week { Number = 1 }, Competitions = [outOfWindow] },
            ],
        };
        _cfbApi.GetScoresByDateRangeAsync(slate.StartDate, slate.EndDate).Returns(scoreboard);

        var result = await BuildFetcher().FetchForSlateAsync(slate, isCurrentSlate: false);

        Assert.NotNull(result);
        var evt = Assert.Single(result!.Events!);
        var comp = evt.Competitions.Single();
        Assert.Equal(TypeName.StatusScheduled, comp.Status.Type.Name);
        Assert.Contains(comp.Competitors, c => c.Team.Abbreviation == "FRES");
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

        var result = await BuildFetcher().FetchForSlateAsync(BuildCfpSlate(), isCurrentSlate: false);

        await _cfbApi.Received(1).GetCfpGamesAsync();
        await _cfbApi.DidNotReceive().GetScoresByDateRangeAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>());
        Assert.NotNull(result);
        Assert.Single(result!.Events!);
    }

    [Fact]
    public async Task FetchForSlateAsync_CfpSlate_ExcludesGame_OutsideSlateDateRange() {
        var wrongRound = BuildScoreboard(date: new DateTimeOffset(2026, 1, 1, 18, 0, 0, TimeSpan.Zero));
        _cfbApi.GetCfpGamesAsync().Returns(wrongRound);

        var result = await BuildFetcher().FetchForSlateAsync(BuildCfpSlate(), isCurrentSlate: false);

        Assert.Null(result);
    }

    // ── Missing EspnWeekNumber: the control table (CfbSeasonWeekConfig.EspnWeekNumber) is
    // non-nullable and CfbSlateSeederJob is the only producer of CfbSlates rows, so every real
    // slate carries a week number. frizat-11t: the regular-season ESPN fetch itself is now
    // date-range-based (slate.StartDate/EndDate), not week-based — EspnWeekNumber is still required
    // as the CfbRanking natural-key component, so a missing value still means the control table
    // wasn't seeded correctly. ──────────────────────────────────────────────────────────────

    private static CfbSlates BuildSlateMissingWeekNumber() => new() {
        Id = 3, Season = 2025, SlateNumber = 1,
        Label = "Week 1", SlateType = "RegularSeason",
        StartDate = new DateOnly(2025, 12, 19), EndDate = new DateOnly(2025, 12, 20),
        EspnWeekNumber = null,
    };

    [Fact]
    public async Task FetchForSlateAsync_MissingEspnWeekNumber_ReturnsNull_NeverCallsEspn() {
        var result = await BuildFetcher().FetchForSlateAsync(BuildSlateMissingWeekNumber(), isCurrentSlate: false);

        Assert.Null(result);
        await _cfbApi.DidNotReceiveWithAnyArgs().GetScoresByDateRangeAsync(default, default);
        await _cfbApi.DidNotReceive().GetCfpGamesAsync();
    }
}
