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

public class CfbSpreadJobTests
{
    private readonly ICfbApiService _cfbApi;
    private readonly IEspnCoreOddsService _oddsService;
    private readonly ICfbRepository _repo;
    private readonly IJobExecutionContext _context;

    public CfbSpreadJobTests()
    {
        _cfbApi = Substitute.For<ICfbApiService>();
        _oddsService = Substitute.For<IEspnCoreOddsService>();
        _repo = Substitute.For<ICfbRepository>();
        _context = Substitute.For<IJobExecutionContext>();
    }

    private CfbSpreadJob BuildJob() => new(_cfbApi, _oddsService, _repo);

    private static CfbSlates BuildSlate(int slateId = 1) => new()
    {
        Id = slateId, Season = 2025, SlateNumber = 1,
        Label = "CFP First Round", SlateType = "FirstRound",
        StartDate = new DateOnly(2025, 12, 19),
        EndDate   = new DateOnly(2025, 12, 20),
    };

    private static EspnScores BuildScoreboard(
        string eventId = "401677183",
        string homeAbbr = "ORE", string awayAbbr = "OSU",
        TypeName status = TypeName.StatusScheduled)
    {
        var competition = new Competition {
            Date = new DateTimeOffset(2025, 12, 19, 18, 0, 0, TimeSpan.Zero),
            Competitors = [
                new Competitor { HomeAway = HomeAway.Home, Score = 0, Team = new EspnTeam { Abbreviation = homeAbbr }, Records = [] },
                new Competitor { HomeAway = HomeAway.Away,  Score = 0, Team = new EspnTeam { Abbreviation = awayAbbr }, Records = [] },
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

    private static EspnCoreOddsItem BuildOdds(string homeSpread = "-7.5", string awaySpread = "+7.5", double ou = 52.5) =>
        new() {
            HomeTeamOdds = new EspnCoreTeamOdds { Current = new EspnCoreTeamOddsDetail { PointSpread = new EspnCorePointSpread { American = homeSpread } } },
            AwayTeamOdds = new EspnCoreTeamOdds { Current = new EspnCoreTeamOddsDetail { PointSpread = new EspnCorePointSpread { American = awaySpread } } },
            OverUnder = ou,
            Provider = new EspnCoreOddsProvider { Name = "ESPN BET" },
        };

    [Fact]
    public async Task Execute_WhenNoActiveSlates_DoesNothing()
    {
        _repo.GetSlatesForSeasonAsync(Arg.Any<int>()).Returns([]);

        await BuildJob().Execute(_context);

        await _cfbApi.DidNotReceive().GetScoresByDateAsync(Arg.Any<DateOnly>());
    }

    [Fact]
    public async Task Execute_WhenNoScheduledGames_SavesNoSpreads()
    {
        _repo.GetSlatesForSeasonAsync(Arg.Any<int>()).Returns([BuildSlate()]);
        _cfbApi.GetScoresByDateAsync(Arg.Any<DateOnly>()).Returns((EspnScores?)null);

        await BuildJob().Execute(_context);

        await _repo.DidNotReceive().AddCfbSpreadsAsync(Arg.Any<IEnumerable<CfbSpreads>>());
    }

    [Fact]
    public async Task Execute_FetchesSpreadForScheduledGame_SavesSpread()
    {
        var slate = BuildSlate();
        _repo.GetSlatesForSeasonAsync(Arg.Any<int>()).Returns([slate]);
        _cfbApi.GetScoresByDateAsync(new DateOnly(2025, 12, 19)).Returns(BuildScoreboard());
        _cfbApi.GetScoresByDateAsync(new DateOnly(2025, 12, 20)).Returns((EspnScores?)null);
        _oddsService.GetCfbEventsWithOddsAsync(401677183, 100).Returns(BuildOdds());

        await BuildJob().Execute(_context);

        await _repo.Received(1).AddCfbSpreadsAsync(
            Arg.Is<IEnumerable<CfbSpreads>>(s => s.Count() == 1));
    }

    [Fact]
    public async Task Execute_ParsesSpreadsCorrectly()
    {
        var slate = BuildSlate();
        _repo.GetSlatesForSeasonAsync(Arg.Any<int>()).Returns([slate]);
        _cfbApi.GetScoresByDateAsync(new DateOnly(2025, 12, 19)).Returns(BuildScoreboard(homeAbbr: "ORE", awayAbbr: "OSU"));
        _cfbApi.GetScoresByDateAsync(new DateOnly(2025, 12, 20)).Returns((EspnScores?)null);
        _oddsService.GetCfbEventsWithOddsAsync(401677183, 100).Returns(BuildOdds("-7.5", "+7.5", 52.5));

        IEnumerable<CfbSpreads>? saved = null;
        await _repo.AddCfbSpreadsAsync(Arg.Do<IEnumerable<CfbSpreads>>(s => saved = s));

        await BuildJob().Execute(_context);

        var spread = saved!.First();
        Assert.Equal("ORE", spread.HomeTeam);
        Assert.Equal("OSU", spread.AwayTeam);
        Assert.Equal(-7.5,  spread.HomeTeamSpread);
        Assert.Equal(7.5,   spread.AwayTeamSpread);
        Assert.Equal(52.5,  spread.OverUnder);
        Assert.Equal(slate.Id, spread.CfbSlateId);
    }

    [Fact]
    public async Task Execute_WhenOddsUnavailable_SkipsGame()
    {
        _repo.GetSlatesForSeasonAsync(Arg.Any<int>()).Returns([BuildSlate()]);
        _cfbApi.GetScoresByDateAsync(new DateOnly(2025, 12, 19)).Returns(BuildScoreboard());
        _cfbApi.GetScoresByDateAsync(new DateOnly(2025, 12, 20)).Returns((EspnScores?)null);
        _oddsService.GetCfbEventsWithOddsAsync(Arg.Any<int>(), 100).Returns((EspnCoreOddsItem?)null);
        _oddsService.GetCfbEventsWithOddsAsync(Arg.Any<int>()).Returns((EspnCoreOddsApiResponse?)null);

        await BuildJob().Execute(_context);

        await _repo.DidNotReceive().AddCfbSpreadsAsync(Arg.Any<IEnumerable<CfbSpreads>>());
    }
}
