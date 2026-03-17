using FourPlayWebApp.Server.Controllers;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Data.Dtos;
using FourPlayWebApp.Shared.Models.Enum;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// Unit tests for LeagueController.AddPicks — specifically the per-game kickoff time guard
/// added as part of bead frizat-d6y (Lock Games By Individual Game Time).
/// </summary>
public class PicksTests
{
    private const int LeagueId = 1;
    private const int Season   = 2024;
    private const int Week     = 2;
    private const string UserId = "user-001";

    // ── Factories ─────────────────────────────────────────────────────────────

    private static UserManager<ApplicationUser> BuildUserManager()
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        return Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);
    }

    /// <summary>
    /// Builds a LeagueController with mocked dependencies.
    /// <paramref name="espnCacheService"/> is injected to supply per-game kickoff times.
    /// </summary>
    private static LeagueController BuildController(
        ILeagueRepository repo,
        IEspnCacheService espnCacheService)
    {
        var controller = new LeagueController(
            new MemoryCache(new MemoryCacheOptions()),
            repo,
            NullLogger<LeagueController>.Instance,
            BuildUserManager(),
            Substitute.For<ISpreadCalculatorBuilder>(),
            espnCacheService);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return controller;
    }

    /// <summary>
    /// Builds a minimal ESPN scores payload with two games:
    /// BUF vs MIA (at <paramref name="bufMiaKickoff"/>) and DAL vs NYG (in the future).
    /// </summary>
    private static EspnScores BuildScores(DateTimeOffset bufMiaKickoff)
    {
        return new EspnScores
        {
            Events =
            [
                new Event
                {
                    Id = "evt1",
                    Season = new Season { Year = Season, Type = 2 },
                    Week = new Week { Number = Week },
                    Date = bufMiaKickoff,
                    Competitions =
                    [
                        new Competition
                        {
                            Id = "c1",
                            Date = bufMiaKickoff,
                            Competitors =
                            [
                                new Competitor { Team = new EspnTeam { Abbreviation = "BUF" }, HomeAway = HomeAway.Home },
                                new Competitor { Team = new EspnTeam { Abbreviation = "MIA" }, HomeAway = HomeAway.Away }
                            ],
                            Status = new EspnStatus { Type = new StatusType { Completed = false } },
                            Odds = []
                        }
                    ]
                }
            ]
        };
    }

    private static NflPickDto MakePick(string team) => new()
    {
        LeagueId = LeagueId,
        UserId   = UserId,
        Team     = team,
        Pick     = PickType.Spread,
        NflWeek  = Week,
        Season   = Season,
    };

    private static NflWeeks MakeNflWeek() => new()
    {
        Id      = 1,
        NflWeek = Week,
        Season  = Season,
    };

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddPicks_WhenGameKickoffHasPassed_ReturnsBadRequest()
    {
        // Arrange — kickoff was 2 hours ago
        var pastKickoff = DateTimeOffset.UtcNow.AddHours(-2);

        var repo = Substitute.For<ILeagueRepository>();
        repo.GetNflWeeksAsync(Season).Returns([MakeNflWeek()]);
        repo.GetUserNflPicksAsync(UserId, LeagueId, Season, Week).Returns([]);

        var espn = Substitute.For<IEspnCacheService>();
        espn.GetScoresAsync().Returns(BuildScores(pastKickoff));

        var controller = BuildController(repo, espn);
        var picks = new[] { MakePick("BUF") };

        // Act
        var result = await controller.AddPicks(picks);

        // Assert — server must reject the pick because kickoff has already passed
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("kicked off", badRequest.Value?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddPicks_WhenGameKickoffIsInFuture_ReturnsOk()
    {
        // Arrange — kickoff is 2 hours from now
        var futureKickoff = DateTimeOffset.UtcNow.AddHours(2);

        var repo = Substitute.For<ILeagueRepository>();
        repo.GetNflWeeksAsync(Season).Returns([MakeNflWeek()]);
        repo.GetUserNflPicksAsync(UserId, LeagueId, Season, Week).Returns([]);
        repo.AddNflPicksAsync(Arg.Any<IEnumerable<NflPicks>>()).Returns(Task.CompletedTask);

        var espn = Substitute.For<IEspnCacheService>();
        espn.GetScoresAsync().Returns(BuildScores(futureKickoff));

        var controller = BuildController(repo, espn);
        var picks = new[] { MakePick("BUF") };

        // Act
        var result = await controller.AddPicks(picks);

        // Assert — pick accepted, returns 1
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(1, ok.Value);
    }

    [Fact]
    public async Task AddPicks_WhenEspnCacheIsEmpty_AllowsPicks()
    {
        // Arrange — ESPN cache cold / unavailable; should not block picks
        var repo = Substitute.For<ILeagueRepository>();
        repo.GetNflWeeksAsync(Season).Returns([MakeNflWeek()]);
        repo.GetUserNflPicksAsync(UserId, LeagueId, Season, Week).Returns([]);
        repo.AddNflPicksAsync(Arg.Any<IEnumerable<NflPicks>>()).Returns(Task.CompletedTask);

        var espn = Substitute.For<IEspnCacheService>();
        espn.GetScoresAsync().Returns((EspnScores?)null);

        var controller = BuildController(repo, espn);
        var picks = new[] { MakePick("BUF") };

        // Act
        var result = await controller.AddPicks(picks);

        // Assert — no ESPN data → fail open, let picks through
        Assert.IsType<OkObjectResult>(result.Result);
    }
}
