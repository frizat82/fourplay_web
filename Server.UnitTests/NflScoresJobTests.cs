using FourPlayWebApp.Server.Jobs;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Data;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Quartz;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// Tests for NflScoresJob — fetches completed game scores from ESPN and upserts
/// them into the database, and also upserts NFL week calendar data.
/// </summary>
public class NflScoresJobTests
{
    private readonly IEspnApiService _espnApi;
    private readonly ILeagueRepository _repo;
    private readonly IJobExecutionContext _context;

    public NflScoresJobTests()
    {
        _espnApi = Substitute.For<IEspnApiService>();
        _repo = Substitute.For<ILeagueRepository>();
        _context = Substitute.For<IJobExecutionContext>();

        // Default: all week-score calls return null so loops terminate cleanly
        _espnApi.GetWeekScores(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>())
                .Returns((EspnScores?)null);

        // Default: season scores return an empty response with no leagues/calendar
        _espnApi.GetSeasonScores(Arg.Any<int>())
                .Returns(new EspnScores { Leagues = Array.Empty<EspnLeague>() });
    }

    private NflScoresJob BuildJob() => new(_espnApi, _repo);

    // -----------------------------------------------------------------------
    // Helper builders
    // -----------------------------------------------------------------------

    private static EspnScores BuildWeekScores(int week, int year, bool isFinal,
        string homeAbbr = "KC", string awayAbbr = "BUF",
        int homeScore = 28, int awayScore = 21)
    {
        var statusName = isFinal ? TypeName.StatusFinal : TypeName.StatusScheduled;

        var competition = new Competition
        {
            Date = new DateTimeOffset(year, 9, 10, 18, 0, 0, TimeSpan.Zero),
            Competitors = new[]
            {
                new Competitor
                {
                    Id = "1",
                    HomeAway = HomeAway.Home,
                    Score = homeScore,
                    Team = new EspnTeam { Abbreviation = homeAbbr },
                    Records = Array.Empty<EspnRecord>()
                },
                new Competitor
                {
                    Id = "2",
                    HomeAway = HomeAway.Away,
                    Score = awayScore,
                    Team = new EspnTeam { Abbreviation = awayAbbr },
                    Records = Array.Empty<EspnRecord>()
                }
            },
            Status = new EspnStatus
            {
                Type = new StatusType { Name = statusName, Completed = isFinal }
            },
            Odds = Array.Empty<Odd>()
        };

        return new EspnScores
        {
            Season = new Season { Year = year, Type = (int)TypeOfSeason.RegularSeason },
            Week = new Week { Number = week },
            Events = new[]
            {
                new Event
                {
                    Id = "401547605",
                    Season = new Season { Year = year, Type = (int)TypeOfSeason.RegularSeason },
                    Week = new Week { Number = week },
                    Date = new DateTimeOffset(year, 9, 10, 18, 0, 0, TimeSpan.Zero),
                    Competitions = new[] { competition }
                }
            }
        };
    }

    // -----------------------------------------------------------------------
    // UpsertNflScoresAsync — called when completed games exist
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Execute_WhenWeekHasCompletedGames_CallsUpsertNflScores()
    {
        // Week 1 of the current year has one final game; all other weeks return null
        var year = DateTime.UtcNow.Year;
        _espnApi.GetWeekScores(1, year, false)
                .Returns(BuildWeekScores(1, year, isFinal: true));

        await BuildJob().Execute(_context);

        await _repo.Received(1).UpsertNflScoresAsync(Arg.Is<List<NflScores>>(list => list.Count > 0));
    }

    [Fact]
    public async Task Execute_WhenWeekHasCompletedGames_PassesCorrectTeamAbbreviations()
    {
        var year = DateTime.UtcNow.Year;
        _espnApi.GetWeekScores(1, year, false)
                .Returns(BuildWeekScores(1, year, isFinal: true,
                    homeAbbr: "SF", awayAbbr: "DAL"));

        List<NflScores>? captured = null;
        await _repo.UpsertNflScoresAsync(Arg.Do<List<NflScores>>(l => captured = l));

        await BuildJob().Execute(_context);

        Assert.NotNull(captured);
        Assert.Contains(captured, s => s.HomeTeam == "SF" && s.AwayTeam == "DAL");
    }

    [Fact]
    public async Task Execute_WhenWeekHasCompletedGames_PassesCorrectScores()
    {
        var year = DateTime.UtcNow.Year;
        _espnApi.GetWeekScores(1, year, false)
                .Returns(BuildWeekScores(1, year, isFinal: true,
                    homeScore: 35, awayScore: 17));

        List<NflScores>? captured = null;
        await _repo.UpsertNflScoresAsync(Arg.Do<List<NflScores>>(l => captured = l));

        await BuildJob().Execute(_context);

        Assert.NotNull(captured);
        Assert.Contains(captured, s => s.HomeTeamScore == 35 && s.AwayTeamScore == 17);
    }

    // -----------------------------------------------------------------------
    // UpsertNflScoresAsync — NOT called when no completed games
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Execute_WhenAllWeekCallsReturnNull_DoesNotCallUpsertScores()
    {
        // Default setup: all GetWeekScores return null

        await BuildJob().Execute(_context);

        await _repo.DidNotReceive().UpsertNflScoresAsync(Arg.Any<List<NflScores>>());
    }

    [Fact]
    public async Task Execute_WhenWeekHasOnlyScheduledGames_DoesNotCallUpsertScores()
    {
        var year = DateTime.UtcNow.Year;
        // Return a scoreboard where the game is NOT final (scheduled)
        _espnApi.GetWeekScores(1, year, false)
                .Returns(BuildWeekScores(1, year, isFinal: false));

        await BuildJob().Execute(_context);

        await _repo.DidNotReceive().UpsertNflScoresAsync(Arg.Any<List<NflScores>>());
    }

    // -----------------------------------------------------------------------
    // Pro Bowl skip — week 4 of post season is skipped
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Execute_PostSeasonWeek4_IsNeverRequested_FromEspn()
    {
        // The job skips j == 4 in the post-season loop; verify it never calls
        // GetWeekScores with week=4 and postSeason=true.
        await BuildJob().Execute(_context);

        await _espnApi.DidNotReceive().GetWeekScores(4, Arg.Any<int>(), true);
    }

    // -----------------------------------------------------------------------
    // GetSeasonScores null / empty guards
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Execute_WhenGetSeasonScoresReturnsNullLeagues_DoesNotCallUpsertWeeks()
    {
        _espnApi.GetSeasonScores(Arg.Any<int>())
                .Returns(new EspnScores { Leagues = null });

        await BuildJob().Execute(_context);

        await _repo.DidNotReceive().UpsertNflWeeksAsync(Arg.Any<List<NflWeeks>>());
    }

    [Fact]
    public async Task Execute_WhenGetSeasonScoresReturnsEmptyLeagues_DoesNotCallUpsertWeeks()
    {
        // Default setup already returns empty leagues

        await BuildJob().Execute(_context);

        await _repo.DidNotReceive().UpsertNflWeeksAsync(Arg.Any<List<NflWeeks>>());
    }

    // -----------------------------------------------------------------------
    // ESPN API exception — job must not rethrow
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Execute_WhenGetWeekScoresThrows_Rethrows()
    {
        // NflScoresJob has no try/catch — an exception from GetWeekScores will
        // propagate; verify the exact behaviour by asserting it does NOT swallow
        // the exception (the job propagates it).
        _espnApi.GetWeekScores(Arg.Any<int>(), Arg.Any<int>(), false)
                .ThrowsAsync(new HttpRequestException("ESPN down"));

        // The job does NOT catch exceptions — it re-throws.
        await Assert.ThrowsAsync<HttpRequestException>(() => BuildJob().Execute(_context));
    }

    // -----------------------------------------------------------------------
    // Season week mapping — correct NflWeek assigned
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Execute_RegularSeasonWeek1_AssignsWeek1ToNflWeek()
    {
        // The job loops weeks 1..18 and breaks when GetWeekScores returns null.
        // Use week 1 so the loop reaches it before hitting a null break.
        var year = DateTime.UtcNow.Year;
        _espnApi.GetWeekScores(1, year, false)
                .Returns(BuildWeekScores(1, year, isFinal: true));
        // Weeks 2+ return null (default) so the loop breaks cleanly after week 1.

        List<NflScores>? captured = null;
        _repo.When(r => r.UpsertNflScoresAsync(Arg.Any<List<NflScores>>()))
             .Do(ci => captured = ci.Arg<List<NflScores>>());

        await BuildJob().Execute(_context);

        Assert.NotNull(captured);
        Assert.All(captured, s => Assert.Equal(1, s.NflWeek));
    }
}
