using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Enum;
using NSubstitute;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// frizat-703.6: CFP week=999/date-filter, ranked-team-filter, and legacy date-range branching
/// extracted from CfbScoresJob into a shared fetcher — both the DB-upsert job and CfbCacheService's
/// live-serving path use this same implementation instead of duplicating the logic.
/// </summary>
public class CfbLiveScoreFetcherTests {
    private readonly ICfbApiService _cfbApi = Substitute.For<ICfbApiService>();
    private CfbLiveScoreFetcher BuildFetcher() => new(_cfbApi);

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
}
