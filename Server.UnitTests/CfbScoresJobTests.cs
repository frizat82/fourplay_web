using FourPlayWebApp.Server.Jobs;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Enum;
using NSubstitute;
using Quartz;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// CFP week=999/date-filter and ranked-team-filter behavior is tested directly in
/// CfbLiveScoreFetcherTests (frizat-703.6) — this file only tests what CfbScoresJob does with
/// whatever the fetcher returns: filter to final-only, upsert. Live-update notification is now
/// CfbCacheService's independent poll (see CfbCacheServiceTests), decoupled from this job's DB
/// write cadence — the old DB-upsert-triggered ICfbScoreChangeNotifier is gone.
/// </summary>
public class CfbScoresJobTests
{
    private readonly ICfbLiveScoreFetcher _fetcher;
    private readonly ICfbRepository _repo;
    private readonly IJobExecutionContext _context;

    public CfbScoresJobTests()
    {
        _fetcher = Substitute.For<ICfbLiveScoreFetcher>();
        _repo = Substitute.For<ICfbRepository>();
        _context = Substitute.For<IJobExecutionContext>();
    }

    private CfbScoresJob BuildJob() => new(_fetcher, _repo);

    // Dates relative to "now" (not a fixed calendar date) so this slate is always "currently
    // active" for the SeasonWindowResolver-based gate CfbScoresJob now checks before fetching —
    // tests below that care about score-parsing/upsert behavior shouldn't also have to fight an
    // unrelated off-season skip just because time passed since these dates were written.
    private static CfbSlates BuildSlate() {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return new() {
            Id = 1, Season = 2025, SlateNumber = 1,
            Label = "Week 1", SlateType = "RegularSeason",
            StartDate = today.AddDays(-1),
            EndDate   = today.AddDays(1),
            EspnWeekNumber = 1,
            ScoringFormat = "Spread",
        };
    }

    private static EspnScores BuildScoreboard(
        string eventId = "401677183",
        string homeAbbr = "ORE", string awayAbbr = "OSU",
        int homeScore = 41, int awayScore = 21,
        TypeName status = TypeName.StatusFinal)
    {
        var competition = new Competition {
            Date = new DateTimeOffset(2025, 12, 19, 18, 0, 0, TimeSpan.Zero),
            Competitors = [
                new Competitor { HomeAway = HomeAway.Home, Score = homeScore, Team = new EspnTeam { Abbreviation = homeAbbr }, Records = [] },
                new Competitor { HomeAway = HomeAway.Away,  Score = awayScore, Team = new EspnTeam { Abbreviation = awayAbbr }, Records = [] },
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

    [Fact]
    public async Task Execute_WhenNoSlates_DoesNothing()
    {
        _repo.GetSlatesForSeasonAsync(Arg.Any<int>()).Returns([]);

        await BuildJob().Execute(_context);

        await _fetcher.DidNotReceive().FetchForSlateAsync(Arg.Any<CfbSlates>());
    }

    // Beyond "slates exist" — the job must also check whether the season is actually happening
    // right now (frizat plan: wobbly-chasing-lynx). Slates for the season can already be seeded
    // while we're deep in the prior season's off-season (Season is a calendar-month cutoff).
    [Fact]
    public async Task Execute_WhenSlatesExistButSeasonNotCurrentlyActive_SkipsFetchEntirely()
    {
        var offSeasonSlate = new CfbSlates {
            Id = 1, Season = 2025, SlateNumber = 4, Label = "Championship", SlateType = "Championship",
            StartDate = new DateOnly(2026, 1, 5), EndDate = new DateOnly(2026, 1, 12),
            EspnWeekNumber = 16, ScoringFormat = "Spread",
        };
        _repo.GetSlatesForSeasonAsync(Arg.Any<int>()).Returns([offSeasonSlate]);

        await BuildJob().Execute(_context);

        await _fetcher.DidNotReceive().FetchForSlateAsync(Arg.Any<CfbSlates>());
        await _repo.DidNotReceive().UpsertCfbScoresAsync(Arg.Any<IEnumerable<CfbScores>>());
    }

    [Fact]
    public async Task Execute_WhenFetcherReturnsNull_SavesNoScores()
    {
        _repo.GetSlatesForSeasonAsync(Arg.Any<int>()).Returns([BuildSlate()]);
        _fetcher.FetchForSlateAsync(Arg.Any<CfbSlates>()).Returns((EspnScores?)null);

        await BuildJob().Execute(_context);

        await _repo.DidNotReceive().UpsertCfbScoresAsync(Arg.Any<IEnumerable<CfbScores>>());
    }

    [Fact]
    public async Task Execute_WhenGameFinal_SavesScore()
    {
        _repo.GetSlatesForSeasonAsync(Arg.Any<int>()).Returns([BuildSlate()]);
        _fetcher.FetchForSlateAsync(Arg.Any<CfbSlates>()).Returns(BuildScoreboard(status: TypeName.StatusFinal));

        await BuildJob().Execute(_context);

        await _repo.Received(1).UpsertCfbScoresAsync(
            Arg.Is<IEnumerable<CfbScores>>(s => s.Count() == 1));
    }

    [Fact]
    public async Task Execute_ParsesFinalScoreCorrectly()
    {
        var slate = BuildSlate();
        _repo.GetSlatesForSeasonAsync(Arg.Any<int>()).Returns([slate]);
        _fetcher.FetchForSlateAsync(Arg.Any<CfbSlates>())
            .Returns(BuildScoreboard(homeAbbr: "ORE", awayAbbr: "OSU", homeScore: 41, awayScore: 21, status: TypeName.StatusFinal));

        IEnumerable<CfbScores>? saved = null;
        await _repo.UpsertCfbScoresAsync(Arg.Do<IEnumerable<CfbScores>>(s => saved = s));

        await BuildJob().Execute(_context);

        var score = saved!.First();
        Assert.Equal("ORE", score.HomeTeam);
        Assert.Equal("OSU", score.AwayTeam);
        Assert.Equal(41,    score.HomeTeamScore);
        Assert.Equal(21,    score.AwayTeamScore);
        Assert.Equal("StatusFinal", score.GameStatus);
        Assert.Equal(slate.Id, score.CfbSlateId);
    }

    // Only FINAL games are ever written to CfbScores — matches NflScoresJob's IsGameOver filter.
    // Live/in-progress display comes from CfbCacheService (frizat-703.6), never from this DB table.
    [Fact]
    public async Task Execute_InProgressGame_IsNotSaved()
    {
        _repo.GetSlatesForSeasonAsync(Arg.Any<int>()).Returns([BuildSlate()]);
        _fetcher.FetchForSlateAsync(Arg.Any<CfbSlates>()).Returns(BuildScoreboard(status: TypeName.StatusInProgress));

        await BuildJob().Execute(_context);

        await _repo.DidNotReceive().UpsertCfbScoresAsync(Arg.Any<IEnumerable<CfbScores>>());
    }

    [Fact]
    public async Task Execute_HalftimeGame_IsNotSaved()
    {
        _repo.GetSlatesForSeasonAsync(Arg.Any<int>()).Returns([BuildSlate()]);
        _fetcher.FetchForSlateAsync(Arg.Any<CfbSlates>()).Returns(BuildScoreboard(status: TypeName.StatusHalftime));

        await BuildJob().Execute(_context);

        await _repo.DidNotReceive().UpsertCfbScoresAsync(Arg.Any<IEnumerable<CfbScores>>());
    }

    [Fact]
    public async Task Execute_ScheduledGame_IsNotSaved()
    {
        _repo.GetSlatesForSeasonAsync(Arg.Any<int>()).Returns([BuildSlate()]);
        _fetcher.FetchForSlateAsync(Arg.Any<CfbSlates>()).Returns(BuildScoreboard(status: TypeName.StatusScheduled));

        await BuildJob().Execute(_context);

        await _repo.DidNotReceive().UpsertCfbScoresAsync(Arg.Any<IEnumerable<CfbScores>>());
    }

    [Fact]
    public async Task Execute_CapturesWeatherWhenPresent()
    {
        var scoreboard = BuildScoreboard(status: TypeName.StatusFinal);
        scoreboard.Events[0].Weather = new EspnWeather {
            DisplayValue = "Partly Cloudy", ConditionId = "3", Temperature = 55
        };
        _repo.GetSlatesForSeasonAsync(Arg.Any<int>()).Returns([BuildSlate()]);
        _fetcher.FetchForSlateAsync(Arg.Any<CfbSlates>()).Returns(scoreboard);

        IEnumerable<CfbScores>? saved = null;
        await _repo.UpsertCfbScoresAsync(Arg.Do<IEnumerable<CfbScores>>(s => saved = s));
        await BuildJob().Execute(_context);

        var score = saved!.First();
        Assert.Equal("Partly Cloudy", score.WeatherDisplayValue);
        Assert.Equal("3",             score.WeatherConditionId);
        Assert.Equal(55,              score.WeatherTemperatureF);
    }

    [Fact]
    public async Task Execute_WeatherIsNullWhenEventHasNoWeather()
    {
        _repo.GetSlatesForSeasonAsync(Arg.Any<int>()).Returns([BuildSlate()]);
        _fetcher.FetchForSlateAsync(Arg.Any<CfbSlates>()).Returns(BuildScoreboard(status: TypeName.StatusFinal));

        IEnumerable<CfbScores>? saved = null;
        await _repo.UpsertCfbScoresAsync(Arg.Do<IEnumerable<CfbScores>>(s => saved = s));
        await BuildJob().Execute(_context);

        var score = saved!.First();
        Assert.Null(score.WeatherDisplayValue);
        Assert.Null(score.WeatherConditionId);
        Assert.Null(score.WeatherTemperatureF);
    }
}
