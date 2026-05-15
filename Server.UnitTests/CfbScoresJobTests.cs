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

public class CfbScoresJobTests
{
    private readonly ICfbApiService _cfbApi;
    private readonly ICfbRepository _repo;
    private readonly IJobExecutionContext _context;

    public CfbScoresJobTests()
    {
        _cfbApi = Substitute.For<ICfbApiService>();
        _repo = Substitute.For<ICfbRepository>();
        _context = Substitute.For<IJobExecutionContext>();
    }

    private CfbScoresJob BuildJob() => new(_cfbApi, _repo);

    private static CfbSlates BuildSlate() => new()
    {
        Id = 1, Season = 2025, SlateNumber = 1,
        Label = "CFP First Round", SlateType = "FirstRound",
        StartDate = new DateOnly(2025, 12, 19),
        EndDate   = new DateOnly(2025, 12, 20),
    };

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

        await _cfbApi.DidNotReceive().GetScoresByDateAsync(Arg.Any<DateOnly>());
    }

    [Fact]
    public async Task Execute_WhenNoCompletedGames_SavesNoScores()
    {
        _repo.GetSlatesForSeasonAsync(Arg.Any<int>()).Returns([BuildSlate()]);
        _cfbApi.GetScoresByDateAsync(Arg.Any<DateOnly>())
            .Returns(BuildScoreboard(status: TypeName.StatusScheduled));

        await BuildJob().Execute(_context);

        await _repo.DidNotReceive().UpsertCfbScoresAsync(Arg.Any<IEnumerable<CfbScores>>());
    }

    [Fact]
    public async Task Execute_WhenGameFinal_SavesScore()
    {
        _repo.GetSlatesForSeasonAsync(Arg.Any<int>()).Returns([BuildSlate()]);
        _cfbApi.GetScoresByDateAsync(new DateOnly(2025, 12, 19))
            .Returns(BuildScoreboard(status: TypeName.StatusFinal));
        _cfbApi.GetScoresByDateAsync(new DateOnly(2025, 12, 20)).Returns((EspnScores?)null);

        await BuildJob().Execute(_context);

        await _repo.Received(1).UpsertCfbScoresAsync(
            Arg.Is<IEnumerable<CfbScores>>(s => s.Count() == 1));
    }

    [Fact]
    public async Task Execute_ParsesFinalScoreCorrectly()
    {
        var slate = BuildSlate();
        _repo.GetSlatesForSeasonAsync(Arg.Any<int>()).Returns([slate]);
        _cfbApi.GetScoresByDateAsync(new DateOnly(2025, 12, 19))
            .Returns(BuildScoreboard(homeAbbr: "ORE", awayAbbr: "OSU", homeScore: 41, awayScore: 21, status: TypeName.StatusFinal));
        _cfbApi.GetScoresByDateAsync(new DateOnly(2025, 12, 20)).Returns((EspnScores?)null);

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

    [Fact]
    public async Task Execute_InProgressGame_IsAlsoSaved()
    {
        _repo.GetSlatesForSeasonAsync(Arg.Any<int>()).Returns([BuildSlate()]);
        _cfbApi.GetScoresByDateAsync(new DateOnly(2025, 12, 19))
            .Returns(BuildScoreboard(status: TypeName.StatusInProgress));
        _cfbApi.GetScoresByDateAsync(new DateOnly(2025, 12, 20)).Returns((EspnScores?)null);

        await BuildJob().Execute(_context);

        await _repo.Received(1).UpsertCfbScoresAsync(Arg.Any<IEnumerable<CfbScores>>());
    }
}
