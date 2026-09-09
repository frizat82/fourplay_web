using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Enum;
using NSubstitute;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// frizat: NflLiveScoreFetcher never trusts ESPN's own week=N bucketing (frizat-11t's NFL
/// mirror) — it queries by the control table's own date window and re-filters downstream as
/// defense in depth, same shape as CfbLiveScoreFetcher's regular-season fetch.
/// </summary>
public class NflLiveScoreFetcherTests {
    private readonly IEspnApiService _espnApi = Substitute.For<IEspnApiService>();
    private NflLiveScoreFetcher BuildFetcher() => new(_espnApi);

    private static NflSeasonWeekConfig BuildWeek(int weekId = 1, int season = 2026, bool postSeason = false) => new() {
        Id = weekId,
        Season = season,
        WeekId = weekId,
        WeekLabel = $"Week {weekId}",
        WeekType = postSeason ? "PostSeason" : "RegularSeason",
        ScoringFormat = "Standard",
        WeekStartDatetime = new DateTime(season, 9, 1, 0, 0, 0, DateTimeKind.Utc).AddDays((weekId - 1) * 7),
        WeekEndDatetime = new DateTime(season, 9, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(weekId * 7),
        SpreadLockDatetime = new DateTime(season, 9, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    private static Event BuildEvent(DateTimeOffset date, string homeAbbr = "KC", string awayAbbr = "BUF") => new() {
        Id = "1",
        Season = new Season { Year = date.Year, Type = (int)TypeOfSeason.RegularSeason },
        Week = new Week { Number = 1 },
        Date = date,
        Competitions = [
            new Competition {
                Date = date,
                Competitors = [
                    new Competitor { HomeAway = HomeAway.Home, Score = 24, Team = new EspnTeam { Abbreviation = homeAbbr }, Records = [] },
                    new Competitor { HomeAway = HomeAway.Away, Score = 17, Team = new EspnTeam { Abbreviation = awayAbbr }, Records = [] },
                ],
                Status = new EspnStatus { Type = new StatusType { Name = TypeName.StatusFinal, Completed = true, Description = Description.Final } },
                Odds = [],
            }
        ],
    };

    [Fact]
    public async Task FetchForWeekAsync_QueriesEspnByTheWeeksOwnDateWindow_NotEspnWeekNumber() {
        var week = BuildWeek(weekId: 3, season: 2026);
        _espnApi.GetScoresByDateRangeAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<bool>())
                .Returns(new EspnScores { Events = [] });

        await BuildFetcher().FetchForWeekAsync(week);

        await _espnApi.Received(1).GetScoresByDateRangeAsync(
            DateOnly.FromDateTime(week.WeekStartDatetime), DateOnly.FromDateTime(week.WeekEndDatetime), false);
    }

    [Fact]
    public async Task FetchForWeekAsync_PostSeasonWeek_PassesPostSeasonTrue() {
        var week = BuildWeek(weekId: 19, season: 2026, postSeason: true);
        _espnApi.GetScoresByDateRangeAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<bool>())
                .Returns(new EspnScores { Events = [] });

        await BuildFetcher().FetchForWeekAsync(week);

        await _espnApi.Received(1).GetScoresByDateRangeAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), true);
    }

    [Fact]
    public async Task FetchForWeekAsync_DropsEventsOutsideTheWeeksWindow_DefenseInDepth() {
        var week = BuildWeek(weekId: 1, season: 2026);
        var inWindow = BuildEvent(new DateTimeOffset(week.WeekStartDatetime.AddDays(1)));
        var outsideWindow = BuildEvent(new DateTimeOffset(week.WeekEndDatetime.AddDays(10)), homeAbbr: "SF", awayAbbr: "DAL");
        _espnApi.GetScoresByDateRangeAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<bool>())
                .Returns(new EspnScores { Events = [inWindow, outsideWindow] });

        var result = await BuildFetcher().FetchForWeekAsync(week);

        Assert.NotNull(result);
        Assert.Single(result!.Events!);
        Assert.Equal("KC", result.Events!.Single().Competitions.First().Competitors.First(c => c.HomeAway == HomeAway.Home).Team.Abbreviation);
    }

    [Fact]
    public async Task FetchForWeekAsync_ReturnsNull_WhenNoEventsSurviveTheWindowFilter() {
        var week = BuildWeek(weekId: 1, season: 2026);
        var outsideWindow = BuildEvent(new DateTimeOffset(week.WeekEndDatetime.AddDays(10)));
        _espnApi.GetScoresByDateRangeAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<bool>())
                .Returns(new EspnScores { Events = [outsideWindow] });

        var result = await BuildFetcher().FetchForWeekAsync(week);

        Assert.Null(result);
    }

    [Fact]
    public async Task FetchForWeekAsync_ReturnsNull_WhenEspnReturnsNull() {
        var week = BuildWeek();
        _espnApi.GetScoresByDateRangeAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<bool>())
                .Returns((EspnScores?)null);

        var result = await BuildFetcher().FetchForWeekAsync(week);

        Assert.Null(result);
    }
}
