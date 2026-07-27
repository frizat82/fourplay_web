using FourPlayWebApp.Server.Jobs;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Enum;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Quartz;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// Tests for NflSpreadJob — fetches odds from the ESPN Core Odds API and
/// persists spread data for games that are still scheduled.
/// </summary>
public class NflSpreadJobTests
{
    private readonly IEspnCoreOddsService _oddsService;
    private readonly IEspnApiService _espnApi;
    private readonly ILeagueRepository _repo;
    private readonly INflCurrentWeekService _nflCurrentWeekService;
    private readonly IJobExecutionContext _context;

    // Default regular-season week used by most tests
    private static readonly NflWeekInfo DefaultWeek = new(5, 5, 2024, false, "Week 5", "Standard");

    public NflSpreadJobTests()
    {
        _oddsService = Substitute.For<IEspnCoreOddsService>();
        _espnApi = Substitute.For<IEspnApiService>();
        _repo = Substitute.For<ILeagueRepository>();
        _nflCurrentWeekService = Substitute.For<INflCurrentWeekService>();
        _context = Substitute.For<IJobExecutionContext>();

        _nflCurrentWeekService.GetCurrentWeekAsync().Returns(DefaultWeek);
    }

    private NflSpreadJob BuildJob() => new(_oddsService, _espnApi, _repo, _nflCurrentWeekService);

    // -----------------------------------------------------------------------
    // Helper builders
    // -----------------------------------------------------------------------

    private static EspnScores BuildScoreboard(
        int weekNumber = 5,
        int seasonType = (int)TypeOfSeason.RegularSeason,
        int year = 2024,
        TypeName statusName = TypeName.StatusScheduled,
        string homeAbbr = "KC",
        string awayAbbr = "BUF",
        string eventId = "401547605",
        bool emptyEvents = false)
    {
        var competition = new Competition
        {
            Date = new DateTimeOffset(year, 11, 10, 18, 0, 0, TimeSpan.Zero),
            Competitors = new[]
            {
                new Competitor
                {
                    Id = "1",
                    HomeAway = HomeAway.Home,
                    Score = 0,
                    Team = new EspnTeam { Abbreviation = homeAbbr },
                    Records = Array.Empty<EspnRecord>()
                },
                new Competitor
                {
                    Id = "2",
                    HomeAway = HomeAway.Away,
                    Score = 0,
                    Team = new EspnTeam { Abbreviation = awayAbbr },
                    Records = Array.Empty<EspnRecord>()
                }
            },
            Status = new EspnStatus
            {
                Type = new StatusType { Name = statusName, Completed = false }
            },
            Odds = Array.Empty<Odd>()
        };

        return new EspnScores
        {
            Season = new Season { Year = year, Type = seasonType },
            Week = new Week { Number = weekNumber },
            Events = emptyEvents ? [] : new[]
            {
                new Event
                {
                    Id = eventId,
                    Season = new Season { Year = year, Type = seasonType },
                    Week = new Week { Number = weekNumber },
                    Date = new DateTimeOffset(year, 11, 10, 18, 0, 0, TimeSpan.Zero),
                    Competitions = new[] { competition }
                }
            }
        };
    }

    private static EspnCoreOddsItem BuildOddsItem(
        string homeSpread = "-7",
        string awaySpread = "+7",
        double overUnder = 48.5)
    {
        return new EspnCoreOddsItem
        {
            Provider = new EspnCoreOddsProvider { Name = "DraftKings", Id = "100" },
            OverUnder = overUnder,
            HomeTeamOdds = new EspnCoreTeamOdds
            {
                Current = new EspnCoreTeamOddsDetail
                {
                    PointSpread = new EspnCorePointSpread { American = homeSpread }
                }
            },
            AwayTeamOdds = new EspnCoreTeamOdds
            {
                Current = new EspnCoreTeamOddsDetail
                {
                    PointSpread = new EspnCorePointSpread { American = awaySpread }
                }
            }
        };
    }

    // -----------------------------------------------------------------------
    // Early-return when GetWeekScores returns null
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Execute_WhenGetWeekScoresReturnsNull_ReturnsImmediately_NoSpreadsAdded()
    {
        _espnApi.GetWeekScores(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>())
                .Returns((EspnScores?)null);

        await BuildJob().Execute(_context);

        await _repo.DidNotReceive().AddNewNflSpreadsAsync(Arg.Any<List<NflSpreads>>());
        await _oddsService.DidNotReceive()
                          .GetEventsWithOddsAsync(Arg.Any<int>(), Arg.Any<int>());
    }

    // -----------------------------------------------------------------------
    // No scheduled games — nothing saved
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Execute_WhenAllGamesAreAlreadyFinal_NoSpreadsAdded()
    {
        // Scoreboard with a Final game — not Scheduled, so job skips it
        _espnApi.GetWeekScores(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>())
                .Returns(BuildScoreboard(statusName: TypeName.StatusFinal));

        await BuildJob().Execute(_context);

        await _repo.DidNotReceive().AddNewNflSpreadsAsync(Arg.Any<List<NflSpreads>>());
    }

    // -----------------------------------------------------------------------
    // Happy path — DraftKings odds available
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Execute_WhenDraftKingsOddsAvailable_CallsAddNewNflSpreads()
    {
        _espnApi.GetWeekScores(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>())
                .Returns(BuildScoreboard());
        _oddsService.GetEventsWithOddsAsync(401547605, (int)EspnOddsProviders.DraftKings)
                    .Returns(BuildOddsItem());

        await BuildJob().Execute(_context);

        await _repo.Received(1).AddNewNflSpreadsAsync(Arg.Is<List<NflSpreads>>(l => l.Count > 0));
    }

    [Fact]
    public async Task Execute_WhenDraftKingsOddsAvailable_ParsesHomeSpreadCorrectly()
    {
        _espnApi.GetWeekScores(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>())
                .Returns(BuildScoreboard());
        _oddsService.GetEventsWithOddsAsync(401547605, (int)EspnOddsProviders.DraftKings)
                    .Returns(BuildOddsItem(homeSpread: "-7", awaySpread: "+7"));

        List<NflSpreads>? captured = null;
        await _repo.AddNewNflSpreadsAsync(Arg.Do<List<NflSpreads>>(l => captured = l));

        await BuildJob().Execute(_context);

        Assert.NotNull(captured);
        Assert.Single(captured);
        Assert.Equal(-7.0, captured[0].HomeTeamSpread);
        Assert.Equal(7.0, captured[0].AwayTeamSpread);
    }

    [Fact]
    public async Task Execute_WhenDraftKingsOddsAvailable_ParsesDecimalSpreadCorrectly()
    {
        _espnApi.GetWeekScores(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>())
                .Returns(BuildScoreboard());
        _oddsService.GetEventsWithOddsAsync(401547605, (int)EspnOddsProviders.DraftKings)
                    .Returns(BuildOddsItem(homeSpread: "-3.5", awaySpread: "+3.5"));

        List<NflSpreads>? captured = null;
        await _repo.AddNewNflSpreadsAsync(Arg.Do<List<NflSpreads>>(l => captured = l));

        await BuildJob().Execute(_context);

        Assert.NotNull(captured);
        Assert.Equal(-3.5, captured[0].HomeTeamSpread);
        Assert.Equal(3.5, captured[0].AwayTeamSpread);
    }

    [Fact]
    public async Task Execute_WhenDraftKingsOddsAvailable_SetsOverUnder()
    {
        _espnApi.GetWeekScores(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>())
                .Returns(BuildScoreboard());
        _oddsService.GetEventsWithOddsAsync(401547605, (int)EspnOddsProviders.DraftKings)
                    .Returns(BuildOddsItem(overUnder: 48.5));

        List<NflSpreads>? captured = null;
        await _repo.AddNewNflSpreadsAsync(Arg.Do<List<NflSpreads>>(l => captured = l));

        await BuildJob().Execute(_context);

        Assert.NotNull(captured);
        Assert.Equal(48.5, captured[0].OverUnder);
    }

    [Fact]
    public async Task Execute_WhenDraftKingsOddsAvailable_PassesCorrectTeamAbbreviations()
    {
        _espnApi.GetWeekScores(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>())
                .Returns(BuildScoreboard(homeAbbr: "SF", awayAbbr: "DAL"));
        _oddsService.GetEventsWithOddsAsync(401547605, (int)EspnOddsProviders.DraftKings)
                    .Returns(BuildOddsItem());

        List<NflSpreads>? captured = null;
        await _repo.AddNewNflSpreadsAsync(Arg.Do<List<NflSpreads>>(l => captured = l));

        await BuildJob().Execute(_context);

        Assert.NotNull(captured);
        Assert.Equal("SF", captured[0].HomeTeam);
        Assert.Equal("DAL", captured[0].AwayTeam);
    }

    // -----------------------------------------------------------------------
    // Plus-sign stripping before parse
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Execute_PlusSignInSpreadString_IsStrippedBeforeParsing()
    {
        // "+7" should be parsed as 7.0 after stripping the leading plus
        _espnApi.GetWeekScores(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>())
                .Returns(BuildScoreboard());
        _oddsService.GetEventsWithOddsAsync(401547605, (int)EspnOddsProviders.DraftKings)
                    .Returns(BuildOddsItem(homeSpread: "+3", awaySpread: "-3"));

        List<NflSpreads>? captured = null;
        await _repo.AddNewNflSpreadsAsync(Arg.Do<List<NflSpreads>>(l => captured = l));

        await BuildJob().Execute(_context);

        Assert.NotNull(captured);
        Assert.Equal(3.0, captured[0].HomeTeamSpread);
        Assert.Equal(-3.0, captured[0].AwayTeamSpread);
    }

    // -----------------------------------------------------------------------
    // FK sentinel value — game skipped (double.TryParse fails for "FK")
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Execute_WhenHomeSpreadIsFk_GameIsSkipped_NoSpreadsAdded()
    {
        // "FK" cannot be parsed by double.TryParse → the game is skipped via `continue`
        _espnApi.GetWeekScores(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>())
                .Returns(BuildScoreboard());
        _oddsService.GetEventsWithOddsAsync(401547605, (int)EspnOddsProviders.DraftKings)
                    .Returns(BuildOddsItem(homeSpread: "FK", awaySpread: "+7"));

        await BuildJob().Execute(_context);

        await _repo.DidNotReceive().AddNewNflSpreadsAsync(Arg.Any<List<NflSpreads>>());
    }

    [Fact]
    public async Task Execute_WhenAwaySpreadIsFk_GameIsSkipped_NoSpreadsAdded()
    {
        _espnApi.GetWeekScores(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>())
                .Returns(BuildScoreboard());
        _oddsService.GetEventsWithOddsAsync(401547605, (int)EspnOddsProviders.DraftKings)
                    .Returns(BuildOddsItem(homeSpread: "-7", awaySpread: "FK"));

        await BuildJob().Execute(_context);

        await _repo.DidNotReceive().AddNewNflSpreadsAsync(Arg.Any<List<NflSpreads>>());
    }

    // -----------------------------------------------------------------------
    // DraftKings unavailable — falls back to all-providers list
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Execute_WhenDraftKingsNull_FallsBackToFirstAvailableProvider()
    {
        _espnApi.GetWeekScores(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>())
                .Returns(BuildScoreboard());

        // DraftKings returns null
        _oddsService.GetEventsWithOddsAsync(401547605, (int)EspnOddsProviders.DraftKings)
                    .Returns((EspnCoreOddsItem?)null);

        // All-providers fallback returns a list with one item
        var fallbackItem = BuildOddsItem(homeSpread: "-6", awaySpread: "+6", overUnder: 45.0);
        fallbackItem.Provider.Name = "Caesars";
        _oddsService.GetEventsWithOddsAsync(401547605)
                    .Returns(new EspnCoreOddsApiResponse
                    {
                        Count = 1,  // job checks allResults.Count == 0 to skip
                        Items = new List<EspnCoreOddsItem> { fallbackItem }
                    });

        await BuildJob().Execute(_context);

        await _repo.Received(1).AddNewNflSpreadsAsync(Arg.Is<List<NflSpreads>>(l => l.Count > 0));
    }

    [Fact]
    public async Task Execute_WhenDraftKingsNullAndFallbackEmpty_GameIsSkipped()
    {
        _espnApi.GetWeekScores(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>())
                .Returns(BuildScoreboard());

        _oddsService.GetEventsWithOddsAsync(401547605, (int)EspnOddsProviders.DraftKings)
                    .Returns((EspnCoreOddsItem?)null);
        _oddsService.GetEventsWithOddsAsync(401547605)
                    .Returns(new EspnCoreOddsApiResponse { Items = new List<EspnCoreOddsItem>() });

        await BuildJob().Execute(_context);

        await _repo.DidNotReceive().AddNewNflSpreadsAsync(Arg.Any<List<NflSpreads>>());
    }

    [Fact]
    public async Task Execute_WhenDraftKingsNullAndFallbackNull_GameIsSkipped()
    {
        _espnApi.GetWeekScores(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>())
                .Returns(BuildScoreboard());

        _oddsService.GetEventsWithOddsAsync(401547605, (int)EspnOddsProviders.DraftKings)
                    .Returns((EspnCoreOddsItem?)null);
        _oddsService.GetEventsWithOddsAsync(401547605)
                    .Returns((EspnCoreOddsApiResponse?)null);

        await BuildJob().Execute(_context);

        await _repo.DidNotReceive().AddNewNflSpreadsAsync(Arg.Any<List<NflSpreads>>());
    }

    // -----------------------------------------------------------------------
    // ESPN odds API exception — per-game exception is caught, job continues
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Execute_WhenOddsApiThrows_ExceptionCaught_JobDoesNotRethrow()
    {
        _espnApi.GetWeekScores(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>())
                .Returns(BuildScoreboard());
        _oddsService.GetEventsWithOddsAsync(401547605, (int)EspnOddsProviders.DraftKings)
                    .ThrowsAsync(new HttpRequestException("Odds API down"));

        // The job has a try/catch per game, so it must not rethrow
        var exception = await Record.ExceptionAsync(() => BuildJob().Execute(_context));
        Assert.Null(exception);
    }

    [Fact]
    public async Task Execute_WhenOddsApiThrowsForOneGame_OtherGamesAreStillProcessed()
    {
        // Two events: first throws, second succeeds
        var scoreboard = new EspnScores
        {
            Season = new Season { Year = 2024, Type = (int)TypeOfSeason.RegularSeason },
            Week = new Week { Number = 5 },
            Events = new[]
            {
                new Event
                {
                    Id = "111",
                    Season = new Season { Year = 2024, Type = (int)TypeOfSeason.RegularSeason },
                    Week = new Week { Number = 5 },
                    Date = new DateTimeOffset(2024, 11, 10, 18, 0, 0, TimeSpan.Zero),
                    Competitions = new[]
                    {
                        new Competition
                        {
                            Date = new DateTimeOffset(2024, 11, 10, 18, 0, 0, TimeSpan.Zero),
                            Competitors = new[]
                            {
                                new Competitor { Id = "1", HomeAway = HomeAway.Home, Score = 0,
                                    Team = new EspnTeam { Abbreviation = "KC" }, Records = Array.Empty<EspnRecord>() },
                                new Competitor { Id = "2", HomeAway = HomeAway.Away, Score = 0,
                                    Team = new EspnTeam { Abbreviation = "BUF" }, Records = Array.Empty<EspnRecord>() }
                            },
                            Status = new EspnStatus { Type = new StatusType { Name = TypeName.StatusScheduled } },
                            Odds = Array.Empty<Odd>()
                        }
                    }
                },
                new Event
                {
                    Id = "222",
                    Season = new Season { Year = 2024, Type = (int)TypeOfSeason.RegularSeason },
                    Week = new Week { Number = 5 },
                    Date = new DateTimeOffset(2024, 11, 10, 18, 0, 0, TimeSpan.Zero),
                    Competitions = new[]
                    {
                        new Competition
                        {
                            Date = new DateTimeOffset(2024, 11, 10, 18, 0, 0, TimeSpan.Zero),
                            Competitors = new[]
                            {
                                new Competitor { Id = "3", HomeAway = HomeAway.Home, Score = 0,
                                    Team = new EspnTeam { Abbreviation = "SF" }, Records = Array.Empty<EspnRecord>() },
                                new Competitor { Id = "4", HomeAway = HomeAway.Away, Score = 0,
                                    Team = new EspnTeam { Abbreviation = "DAL" }, Records = Array.Empty<EspnRecord>() }
                            },
                            Status = new EspnStatus { Type = new StatusType { Name = TypeName.StatusScheduled } },
                            Odds = Array.Empty<Odd>()
                        }
                    }
                }
            }
        };

        _espnApi.GetWeekScores(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>()).Returns(scoreboard);

        // First game throws
        _oddsService.GetEventsWithOddsAsync(111, (int)EspnOddsProviders.DraftKings)
                    .ThrowsAsync(new HttpRequestException("Odds API down"));

        // Second game succeeds
        _oddsService.GetEventsWithOddsAsync(222, (int)EspnOddsProviders.DraftKings)
                    .Returns(BuildOddsItem(homeSpread: "-3", awaySpread: "+3", overUnder: 47.0));

        await BuildJob().Execute(_context);

        // Second game's spread should be persisted
        await _repo.Received(1).AddNewNflSpreadsAsync(
            Arg.Is<List<NflSpreads>>(l => l.Count == 1 && l[0].HomeTeam == "SF"));
    }

    // -----------------------------------------------------------------------
    // Bye week detection (Super Bowl off-week — zero scheduled competitions)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Execute_WhenZeroCompetitions_ByeWeekDetected_NoSpreadsAdded()
    {
        _espnApi.GetWeekScores(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>())
                .Returns(BuildScoreboard(emptyEvents: true));

        await BuildJob().Execute(_context);

        await _repo.DidNotReceive().AddNewNflSpreadsAsync(Arg.Any<List<NflSpreads>>());
        await _oddsService.DidNotReceive().GetEventsWithOddsAsync(Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public async Task Execute_WhenZeroCompetitions_JobCompletesWithoutException()
    {
        _espnApi.GetWeekScores(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>())
                .Returns(BuildScoreboard(emptyEvents: true));

        var exception = await Record.ExceptionAsync(() => BuildJob().Execute(_context));

        Assert.Null(exception);
    }

    [Fact]
    public async Task Execute_RegularSeasonWeekWithGames_IsNotTreatedAsByeWeek()
    {
        _espnApi.GetWeekScores(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>())
                .Returns(BuildScoreboard(weekNumber: 5, seasonType: (int)TypeOfSeason.RegularSeason));
        _oddsService.GetEventsWithOddsAsync(401547605, (int)EspnOddsProviders.DraftKings)
                    .Returns(BuildOddsItem());

        await BuildJob().Execute(_context);

        await _repo.Received(1).AddNewNflSpreadsAsync(Arg.Is<List<NflSpreads>>(l => l.Count > 0));
    }

    [Fact]
    public async Task Execute_PostSeasonConferenceChampionshipWeek_IsNotTreatedAsByeWeek()
    {
        _nflCurrentWeekService.GetCurrentWeekAsync()
            .Returns(new NflWeekInfo(21, 3, 2024, true, "Conference Championship", "Standard"));
        _espnApi.GetWeekScores(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>())
                .Returns(BuildScoreboard(weekNumber: 3, seasonType: (int)TypeOfSeason.PostSeason));
        _oddsService.GetEventsWithOddsAsync(401547605, (int)EspnOddsProviders.DraftKings)
                    .Returns(BuildOddsItem());

        await BuildJob().Execute(_context);

        await _repo.Received(1).AddNewNflSpreadsAsync(Arg.Is<List<NflSpreads>>(l => l.Count > 0));
    }

    // -----------------------------------------------------------------------
    // Post-season week mapping — WeekId from NflCurrentWeekService drives NflWeek on spread
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Execute_PostSeasonWeek1_MapsToWeek19()
    {
        // Wild Card: NflCurrentWeekService returns WeekId=19, EspnWeek=1, IsPostSeason=true
        _nflCurrentWeekService.GetCurrentWeekAsync()
            .Returns(new NflWeekInfo(19, 1, 2024, true, "Wild Card Weekend", "Standard"));
        _espnApi.GetWeekScores(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>())
                .Returns(BuildScoreboard(weekNumber: 1, seasonType: (int)TypeOfSeason.PostSeason));
        _oddsService.GetEventsWithOddsAsync(401547605, (int)EspnOddsProviders.DraftKings)
                    .Returns(BuildOddsItem());

        List<NflSpreads>? captured = null;
        await _repo.AddNewNflSpreadsAsync(Arg.Do<List<NflSpreads>>(l => captured = l));

        await BuildJob().Execute(_context);

        Assert.NotNull(captured);
        Assert.Single(captured);
        Assert.Equal(19, captured[0].NflWeek);
    }
}
