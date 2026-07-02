using FourPlayWebApp.Server.Controllers;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models.Enum;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;
using System.Security.Claims;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// Smoke tests for LeaderboardController — verifies routing to NFL vs CFB
/// leaderboard services based on LeagueType, caching, and not-found handling.
/// </summary>
public class LeaderboardControllerTests
{
    private readonly ILeaderboardService    _nflService;
    private readonly ICfbLeaderboardService _cfbService;
    private readonly ILeagueRepository     _leagueRepo;

    public LeaderboardControllerTests()
    {
        _nflService  = Substitute.For<ILeaderboardService>();
        _cfbService  = Substitute.For<ICfbLeaderboardService>();
        _leagueRepo  = Substitute.For<ILeagueRepository>();
    }

    // Each test gets its own fresh MemoryCache so caching state doesn't bleed between tests.
    private LeaderboardController BuildController(IMemoryCache? cache = null)
    {
        var controller = new LeaderboardController(
            _nflService,
            _cfbService,
            _leagueRepo,
            cache ?? new MemoryCache(new MemoryCacheOptions()));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, "user-1")], "Test"))
            }
        };
        return controller;
    }

    private static ApplicationUser BuildUser(string id = "u1") =>
        new() { Id = id, UserName = "user" + id };

    private static List<LeaderboardModel> BuildLeaderboard(int count = 2)
    {
        return Enumerable.Range(1, count)
            .Select(i => new LeaderboardModel
            {
                User = BuildUser($"u{i}"),
                Total = i * 100,
                Rank = i.ToString(),
                WeekResults = []
            })
            .ToList();
    }

    // ── NFL league → ILeaderboardService ─────────────────────────────────────

    [Fact]
    public async Task GetLeaderboard_ReturnsOk_WithLeaderboardData_ForNflLeague()
    {
        const int leagueId = 1;
        const long seasonYear = 2025;
        var league = new LeagueInfo { Id = leagueId, LeagueType = LeagueType.Nfl, LeagueName = "NFL Test", OwnerUserId = "owner" };
        var models = BuildLeaderboard();

        _leagueRepo.GetLeagueInfoAsync(leagueId).Returns(league);
        _nflService.BuildLeaderboard(leagueId, seasonYear).Returns(models);

        var result = await BuildController().GetLeaderboard(leagueId, seasonYear);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsAssignableFrom<System.Collections.Generic.List<FourPlayWebApp.Shared.Models.Dtos.LeaderboardDto>>(ok.Value);
        Assert.Equal(models.Count, dtos.Count);
    }

    // ── CFB league → ICfbLeaderboardService ──────────────────────────────────

    [Fact]
    public async Task GetLeaderboard_ReturnsOk_WithLeaderboardData_ForCfbLeague()
    {
        const int leagueId = 2;
        const long seasonYear = 2025;
        var league = new LeagueInfo { Id = leagueId, LeagueType = LeagueType.Cfb, LeagueName = "CFB Test", OwnerUserId = "owner" };
        var models = BuildLeaderboard(3);

        _leagueRepo.GetLeagueInfoAsync(leagueId).Returns(league);
        _cfbService.BuildLeaderboard(leagueId, (int)seasonYear).Returns(models);

        var result = await BuildController().GetLeaderboard(leagueId, seasonYear);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsAssignableFrom<System.Collections.Generic.List<FourPlayWebApp.Shared.Models.Dtos.LeaderboardDto>>(ok.Value);
        Assert.Equal(3, dtos.Count);
    }

    // ── CFB routes to CFB service (not NFL) ──────────────────────────────────

    [Fact]
    public async Task GetLeaderboard_CallsCfbService_NotNflService_ForCfbLeague()
    {
        const int leagueId = 3;
        const long seasonYear = 2025;
        var league = new LeagueInfo { Id = leagueId, LeagueType = LeagueType.Cfb, LeagueName = "CFB", OwnerUserId = "owner" };

        _leagueRepo.GetLeagueInfoAsync(leagueId).Returns(league);
        _cfbService.BuildLeaderboard(leagueId, (int)seasonYear).Returns(BuildLeaderboard(1));

        await BuildController().GetLeaderboard(leagueId, seasonYear);

        await _cfbService.Received(1).BuildLeaderboard(leagueId, (int)seasonYear);
        await _nflService.DidNotReceive().BuildLeaderboard(Arg.Any<int>(), Arg.Any<long>());
    }

    // ── DTO mapping ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetLeaderboard_MapsUserNameAndTotal_Correctly()
    {
        const int leagueId = 4;
        const long seasonYear = 2025;
        var league = new LeagueInfo { Id = leagueId, LeagueType = LeagueType.Nfl, LeagueName = "NFL", OwnerUserId = "owner" };
        var user = new ApplicationUser { Id = "u99", UserName = "Alice" };
        var models = new List<LeaderboardModel>
        {
            new() { User = user, Total = 500, Rank = "1", WeekResults = [] }
        };

        _leagueRepo.GetLeagueInfoAsync(leagueId).Returns(league);
        _nflService.BuildLeaderboard(leagueId, seasonYear).Returns(models);

        var result = await BuildController().GetLeaderboard(leagueId, seasonYear);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsAssignableFrom<System.Collections.Generic.List<FourPlayWebApp.Shared.Models.Dtos.LeaderboardDto>>(ok.Value);
        Assert.Single(dtos);
        Assert.Equal("u99", dtos[0].UserId);
        Assert.Equal("Alice", dtos[0].UserName);
        Assert.Equal(500, dtos[0].Total);
        Assert.Equal("1", dtos[0].Rank);
    }

    // ── NotFound when service returns null ────────────────────────────────────

    [Fact]
    public async Task GetLeaderboard_ReturnsNotFound_WhenServiceReturnsNull()
    {
        const int leagueId = 5;
        const long seasonYear = 2025;
        var league = new LeagueInfo { Id = leagueId, LeagueType = LeagueType.Nfl, LeagueName = "NFL", OwnerUserId = "owner" };

        _leagueRepo.GetLeagueInfoAsync(leagueId).Returns(league);
        _nflService.BuildLeaderboard(leagueId, seasonYear).Returns(Task.FromResult<List<LeaderboardModel>?>(null));

        var result = await BuildController().GetLeaderboard(leagueId, seasonYear);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ── Memory cache — second call does NOT re-invoke service ─────────────────

    [Fact]
    public async Task GetLeaderboard_ReturnsCachedResult_OnSecondCall()
    {
        const int leagueId = 6;
        const long seasonYear = 2025;
        var league = new LeagueInfo { Id = leagueId, LeagueType = LeagueType.Nfl, LeagueName = "NFL", OwnerUserId = "owner" };

        _leagueRepo.GetLeagueInfoAsync(leagueId).Returns(league);
        _nflService.BuildLeaderboard(leagueId, seasonYear).Returns(BuildLeaderboard(2));

        // Shared cache so the second call hits the same cache entry
        var sharedCache = new MemoryCache(new MemoryCacheOptions());
        var controller = BuildController(sharedCache);

        await controller.GetLeaderboard(leagueId, seasonYear);
        await controller.GetLeaderboard(leagueId, seasonYear);

        // Service should only be called once — second call served from cache
        await _nflService.Received(1).BuildLeaderboard(leagueId, seasonYear);
    }
}
