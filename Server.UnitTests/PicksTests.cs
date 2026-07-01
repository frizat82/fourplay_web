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
using System.Security.Claims;

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
        IEspnCacheService espnCacheService,
        ClaimsPrincipal? principal = null,
        IMemoryCache? cache = null)
    {
        var controller = new LeagueController(
            cache ?? new MemoryCache(new MemoryCacheOptions()),
            repo,
            NullLogger<LeagueController>.Instance,
            BuildUserManager(),
            Substitute.For<ISpreadCalculatorBuilder>(),
            espnCacheService,
            Substitute.For<IInvitationService>());

        var httpContext = new DefaultHttpContext();
        if (principal is not null)
            httpContext.User = principal;

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
        return controller;
    }

    private static ClaimsPrincipal BuildPrincipal(string userId) =>
        TestPrincipalFactory.Build(userId);

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
        repo.UserExistsInLeagueAsync(UserId, LeagueId).Returns(true);
        repo.GetUserNflPicksAsync(UserId, LeagueId, Season, Week).Returns([]);

        var espn = Substitute.For<IEspnCacheService>();
        espn.GetScoresAsync().Returns(BuildScores(pastKickoff));

        var controller = BuildController(repo, espn, BuildPrincipal(UserId));
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
        repo.UserExistsInLeagueAsync(UserId, LeagueId).Returns(true);
        repo.GetUserNflPicksAsync(UserId, LeagueId, Season, Week).Returns([]);
        repo.AddNflPicksAsync(Arg.Any<IEnumerable<NflPicks>>()).Returns(Task.CompletedTask);

        var espn = Substitute.For<IEspnCacheService>();
        espn.GetScoresAsync().Returns(BuildScores(futureKickoff));

        var controller = BuildController(repo, espn, BuildPrincipal(UserId));
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
        repo.UserExistsInLeagueAsync(UserId, LeagueId).Returns(true);
        repo.GetUserNflPicksAsync(UserId, LeagueId, Season, Week).Returns([]);
        repo.AddNflPicksAsync(Arg.Any<IEnumerable<NflPicks>>()).Returns(Task.CompletedTask);

        var espn = Substitute.For<IEspnCacheService>();
        espn.GetScoresAsync().Returns((EspnScores?)null);

        var controller = BuildController(repo, espn, BuildPrincipal(UserId));
        var picks = new[] { MakePick("BUF") };

        // Act
        var result = await controller.AddPicks(picks);

        // Assert — no ESPN data → fail open, let picks through
        Assert.IsType<OkObjectResult>(result.Result);
    }

    /// <summary>
    /// frizat-8n3: AddPicks must use the authenticated user's ID from the JWT claim,
    /// not the UserId in the request DTO. Without the fix, a user can submit picks on
    /// behalf of another user by setting UserId in the JSON body.
    /// </summary>
    [Fact]
    public async Task AddPicks_UsesJwtClaimUserId_NotDtoUserId()
    {
        // Arrange — JWT says "jwt-user-id", but DTO has "attacker-user-id"
        const string jwtUserId = "jwt-user-id";
        const string attackerUserId = "attacker-user-id";

        var repo = Substitute.For<ILeagueRepository>();
        repo.GetNflWeeksAsync(Season).Returns([MakeNflWeek()]);
        repo.UserExistsInLeagueAsync(jwtUserId, LeagueId).Returns(true);
        // After the fix the controller will use jwtUserId; set up mock for that userId
        repo.GetUserNflPicksAsync(jwtUserId, LeagueId, Season, Week).Returns([]);
        repo.AddNflPicksAsync(Arg.Any<IEnumerable<NflPicks>>()).Returns(Task.CompletedTask);

        var espn = Substitute.For<IEspnCacheService>();
        espn.GetScoresAsync().Returns((EspnScores?)null);

        var principal = BuildPrincipal(jwtUserId);
        var controller = BuildController(repo, espn, principal);

        // Pick DTO has attacker's userId
        var pick = new NflPickDto
        {
            LeagueId = LeagueId,
            UserId   = attackerUserId,
            Team     = "BUF",
            Pick     = PickType.Spread,
            NflWeek  = Week,
            Season   = Season,
        };

        // Act
        var result = await controller.AddPicks([pick]);

        // Assert — picks saved must use the JWT claim userId, not the DTO value
        await repo.Received(1).AddNflPicksAsync(
            Arg.Is<IEnumerable<NflPicks>>(picks =>
                picks.All(p => p.UserId == jwtUserId)));
    }

    /// <summary>
    /// frizat-8n3: After AddPicks writes successfully, the picks cache for that
    /// league/season/week must be invalidated so the next read reflects the new picks.
    /// </summary>
    [Fact]
    public async Task AddPicks_AfterSuccessfulWrite_InvalidatesPicksCache()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var cacheKey = $"picks_{LeagueId}_{Season}_{Week}";

        // Prime the cache with a stale list
        cache.Set(cacheKey, new List<NflPickDto> { new() { Team = "STALE" } });
        Assert.True(cache.TryGetValue(cacheKey, out _), "Cache must be primed before AddPicks");

        var futureKickoff = DateTimeOffset.UtcNow.AddHours(2);

        var repo = Substitute.For<ILeagueRepository>();
        repo.GetNflWeeksAsync(Season).Returns([MakeNflWeek()]);
        repo.UserExistsInLeagueAsync(UserId, LeagueId).Returns(true);
        repo.GetUserNflPicksAsync(UserId, LeagueId, Season, Week).Returns([]);
        repo.AddNflPicksAsync(Arg.Any<IEnumerable<NflPicks>>()).Returns(Task.CompletedTask);

        var espn = Substitute.For<IEspnCacheService>();
        espn.GetScoresAsync().Returns(BuildScores(futureKickoff));

        var controller = BuildController(repo, espn, BuildPrincipal(UserId), cache);

        await controller.AddPicks([MakePick("BUF")]);

        // Cache entry must be removed so subsequent reads reload from DB
        Assert.False(cache.TryGetValue(cacheKey, out _), "Cache must be cleared after AddPicks");
    }

    /// <summary>
    /// frizat-8n3: AddPicks must return 403 when the authenticated user is not a member
    /// of the league they are submitting picks for (IDOR prevention).
    /// </summary>
    [Fact]
    public async Task AddPicks_ReturnsForbid_WhenUserNotInLeague()
    {
        var repo = Substitute.For<ILeagueRepository>();
        repo.UserExistsInLeagueAsync(UserId, LeagueId).Returns(false);

        var espn = Substitute.For<IEspnCacheService>();
        var controller = BuildController(repo, espn, BuildPrincipal(UserId));

        var result = await controller.AddPicks([MakePick("BUF")]);

        Assert.IsType<ForbidResult>(result.Result);
        await repo.DidNotReceive().AddNflPicksAsync(Arg.Any<IEnumerable<NflPicks>>());
    }

    /// <summary>
    /// frizat-8n3: AddPicks must not insert duplicate picks when the same pick already
    /// exists — Except() on entity objects uses reference equality and always fails,
    /// so we now deduplicate by (Team, NflWeek, Season, LeagueId) key.
    /// </summary>
    [Fact]
    public async Task AddPicks_DeduplicatesByKey_NotReferenceEquality()
    {
        var existingPick = new NflPicks
        {
            LeagueId = LeagueId, UserId = UserId, Team = "BUF",
            Pick = PickType.Spread, NflWeek = Week, Season = Season,
            NflWeekId = 1
        };

        var repo = Substitute.For<ILeagueRepository>();
        repo.GetNflWeeksAsync(Season).Returns([MakeNflWeek()]);
        repo.UserExistsInLeagueAsync(UserId, LeagueId).Returns(true);
        repo.GetUserNflPicksAsync(UserId, LeagueId, Season, Week).Returns([existingPick]);
        repo.AddNflPicksAsync(Arg.Any<IEnumerable<NflPicks>>()).Returns(Task.CompletedTask);

        var espn = Substitute.For<IEspnCacheService>();
        espn.GetScoresAsync().Returns((EspnScores?)null);

        var controller = BuildController(repo, espn, BuildPrincipal(UserId));

        // Submit the same pick that already exists
        var result = await controller.AddPicks([MakePick("BUF")]);

        // Must be OK (not a bad request) but no new picks inserted
        Assert.IsType<OkObjectResult>(result.Result);
        await repo.Received(1).AddNflPicksAsync(
            Arg.Is<IEnumerable<NflPicks>>(picks => !picks.Any()));
    }

    // ── GetLeaguePicks — pick reveal gate ─────────────────────────────────────

    private const string OtherUserId = "other-user-002";

    /// <summary>
    /// Builds an ESPN payload where BUF/MIA is STATUS_SCHEDULED (not started)
    /// and DAL/NYG is STATUS_IN_PROGRESS (already kicked off).
    /// </summary>
    private static EspnScores BuildMixedStatusScores() => new()
    {
        Events =
        [
            new Event
            {
                Id = "e1", Season = new Season { Year = Season, Type = 2 }, Week = new Week { Number = Week },
                Date = DateTimeOffset.UtcNow.AddHours(2),
                Competitions =
                [
                    new Competition
                    {
                        Id = "c1", Date = DateTimeOffset.UtcNow.AddHours(2),
                        Competitors =
                        [
                            new Competitor { Team = new EspnTeam { Abbreviation = "BUF" }, HomeAway = HomeAway.Home },
                            new Competitor { Team = new EspnTeam { Abbreviation = "MIA" }, HomeAway = HomeAway.Away }
                        ],
                        Status = new EspnStatus { Type = new StatusType { Name = TypeName.StatusScheduled, Completed = false } },
                        Odds = []
                    }
                ]
            },
            new Event
            {
                Id = "e2", Season = new Season { Year = Season, Type = 2 }, Week = new Week { Number = Week },
                Date = DateTimeOffset.UtcNow.AddHours(-1),
                Competitions =
                [
                    new Competition
                    {
                        Id = "c2", Date = DateTimeOffset.UtcNow.AddHours(-1),
                        Competitors =
                        [
                            new Competitor { Team = new EspnTeam { Abbreviation = "DAL" }, HomeAway = HomeAway.Home },
                            new Competitor { Team = new EspnTeam { Abbreviation = "NYG" }, HomeAway = HomeAway.Away }
                        ],
                        Status = new EspnStatus { Type = new StatusType { Name = TypeName.StatusInProgress, Completed = false } },
                        Odds = []
                    }
                ]
            }
        ]
    };

    private static NflPicks MakeNflPick(string userId, string team) => new()
    {
        LeagueId = LeagueId, UserId = userId, Team = team,
        Pick = PickType.Spread, NflWeek = Week, Season = Season, NflWeekId = 1,
        User = new ApplicationUser { UserName = userId }
    };

    private static ILeagueRepository BuildRepoWithPicks(params NflPicks[] picks)
    {
        var repo = Substitute.For<ILeagueRepository>();
        repo.UserExistsInLeagueAsync(UserId, LeagueId).Returns(true);
        repo.GetNflPicksAsync(LeagueId, Season, Week).Returns([.. picks]);
        return repo;
    }

    /// <summary>
    /// Other users' picks for a game with STATUS_SCHEDULED must not be returned
    /// to the caller — same rule as revealPicksForStartedGames on the frontend.
    /// </summary>
    [Fact]
    public async Task GetLeaguePicks_HidesOtherUserPicksForScheduledGames()
    {
        var repo = BuildRepoWithPicks(
            MakeNflPick(UserId,      "BUF"),  // caller's own pick — always visible
            MakeNflPick(OtherUserId, "MIA")); // other user on same not-started game

        var espn = Substitute.For<IEspnCacheService>();
        espn.GetScoresAsync().Returns(BuildMixedStatusScores());

        var controller = BuildController(repo, espn, BuildPrincipal(UserId));

        var result = await controller.GetLeaguePicks(LeagueId, Season, Week);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsAssignableFrom<IEnumerable<NflPickDto>>(ok.Value);
        Assert.All(returned, p => Assert.Equal(UserId, p.UserId));
        Assert.DoesNotContain(returned, p => p.UserId == OtherUserId && p.Team == "MIA");
    }

    /// <summary>
    /// Other users' picks for games with STATUS_IN_PROGRESS must be returned — the
    /// game has started so picks are no longer secret.
    /// </summary>
    [Fact]
    public async Task GetLeaguePicks_ShowsOtherUserPicksForStartedGames()
    {
        var repo = BuildRepoWithPicks(
            MakeNflPick(UserId,      "DAL"),  // caller pick on in-progress game
            MakeNflPick(OtherUserId, "NYG")); // other user on same in-progress game

        var espn = Substitute.For<IEspnCacheService>();
        espn.GetScoresAsync().Returns(BuildMixedStatusScores());

        var controller = BuildController(repo, espn, BuildPrincipal(UserId));

        var result = await controller.GetLeaguePicks(LeagueId, Season, Week);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsAssignableFrom<IEnumerable<NflPickDto>>(ok.Value).ToList();
        Assert.Contains(returned, p => p.UserId == OtherUserId && p.Team == "NYG");
    }

    /// <summary>
    /// The caller's own picks are always visible — even when their game is still STATUS_SCHEDULED.
    /// </summary>
    [Fact]
    public async Task GetLeaguePicks_AlwaysShowsCallerOwnPicks()
    {
        var repo = BuildRepoWithPicks(MakeNflPick(UserId, "BUF")); // scheduled game

        var espn = Substitute.For<IEspnCacheService>();
        espn.GetScoresAsync().Returns(BuildMixedStatusScores());

        var controller = BuildController(repo, espn, BuildPrincipal(UserId));

        var result = await controller.GetLeaguePicks(LeagueId, Season, Week);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsAssignableFrom<IEnumerable<NflPickDto>>(ok.Value).ToList();
        Assert.Single(returned);
        Assert.Equal("BUF", returned[0].Team);
    }

    /// <summary>
    /// When the ESPN cache is unavailable, GetLeaguePicks must fail open and return
    /// all picks rather than silently hiding them.
    /// </summary>
    [Fact]
    public async Task GetLeaguePicks_WhenEspnCacheNull_ReturnsAllPicks()
    {
        var repo = BuildRepoWithPicks(
            MakeNflPick(UserId,      "BUF"),
            MakeNflPick(OtherUserId, "MIA"));

        var espn = Substitute.For<IEspnCacheService>();
        espn.GetScoresAsync().Returns((EspnScores?)null);

        var controller = BuildController(repo, espn, BuildPrincipal(UserId));

        var result = await controller.GetLeaguePicks(LeagueId, Season, Week);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsAssignableFrom<IEnumerable<NflPickDto>>(ok.Value).ToList();
        Assert.Equal(2, returned.Count);
    }

    /// <summary>
    /// Admins bypass the pick-reveal filter and always receive all picks regardless
    /// of game status.
    /// </summary>
    [Fact]
    public async Task GetLeaguePicks_AdminSeesAllPicksRegardlessOfGameStatus()
    {
        var repo = Substitute.For<ILeagueRepository>();
        // Admin is NOT in the league — but should still get picks
        repo.UserExistsInLeagueAsync(Arg.Any<string>(), LeagueId).Returns(false);
        repo.GetNflPicksAsync(LeagueId, Season, Week).Returns(
        [
            MakeNflPick(UserId,      "BUF"),  // scheduled game
            MakeNflPick(OtherUserId, "MIA"),  // scheduled game
        ]);

        var espn = Substitute.For<IEspnCacheService>();
        espn.GetScoresAsync().Returns(BuildMixedStatusScores());

        var adminPrincipal = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
            [
                new System.Security.Claims.Claim(ClaimTypes.NameIdentifier, "admin-001"),
                new System.Security.Claims.Claim(ClaimTypes.Role, "Administrator"),
            ], "Test"));

        var controller = BuildController(repo, espn, adminPrincipal);

        var result = await controller.GetLeaguePicks(LeagueId, Season, Week);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsAssignableFrom<IEnumerable<NflPickDto>>(ok.Value).ToList();
        Assert.Equal(2, returned.Count);
    }

    /// <summary>
    /// ESPN says STATUS_SCHEDULED but the scheduled kickoff time is in the past — the
    /// cache is just stale. Picks must be revealed because the game has de facto started.
    /// </summary>
    [Fact]
    public async Task GetLeaguePicks_ShowsOtherPicksForScheduledStatus_WhenKickoffTimeHasPassed()
    {
        var repo = BuildRepoWithPicks(
            MakeNflPick(UserId,      "BUF"),
            MakeNflPick(OtherUserId, "MIA"));

        // Build a stale-cache scenario: ESPN still says SCHEDULED but Date is 30 min ago
        var staleScheduled = new EspnScores
        {
            Events =
            [
                new Event
                {
                    Id = "e1", Season = new Season { Year = Season, Type = 2 }, Week = new Week { Number = Week },
                    Date = DateTimeOffset.UtcNow.AddMinutes(-30),
                    Competitions =
                    [
                        new Competition
                        {
                            Id = "c1", Date = DateTimeOffset.UtcNow.AddMinutes(-30),
                            Competitors =
                            [
                                new Competitor { Team = new EspnTeam { Abbreviation = "BUF" }, HomeAway = HomeAway.Home },
                                new Competitor { Team = new EspnTeam { Abbreviation = "MIA" }, HomeAway = HomeAway.Away }
                            ],
                            Status = new EspnStatus { Type = new StatusType { Name = TypeName.StatusScheduled, Completed = false } },
                            Odds = []
                        }
                    ]
                }
            ]
        };

        var espn = Substitute.For<IEspnCacheService>();
        espn.GetScoresAsync().Returns(staleScheduled);

        var controller = BuildController(repo, espn, BuildPrincipal(UserId));

        var result = await controller.GetLeaguePicks(LeagueId, Season, Week);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsAssignableFrom<IEnumerable<NflPickDto>>(ok.Value).ToList();
        // Both picks must be visible — the game has kicked off even though ESPN cache is stale
        Assert.Equal(2, returned.Count);
    }
}
