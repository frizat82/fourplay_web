using FourPlayWebApp.Server.Controllers;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Data.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// mon.11: NFL scores/spreads GET endpoints must return DTOs, not raw EF entities.
/// </summary>
public class LeagueControllerDtoTests
{
    private static LeagueController BuildController(ILeagueRepository repo)
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        var userManager = Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);

        var controller = new LeagueController(
            new MemoryCache(new MemoryCacheOptions()),
            repo,
            NullLogger<LeagueController>.Instance,
            userManager,
            Substitute.For<ISpreadCalculatorBuilder>(),
            Substitute.For<IEspnCacheService>(),
            Substitute.For<IInvitationService>(),
            Substitute.For<ILeagueInviteLinkService>());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = TestPrincipalFactory.Build("user-1") }
        };
        return controller;
    }

    // ── NflScoreDto ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetScores_ReturnsNflScoreDtoList_NotRawEntity()
    {
        var repo = Substitute.For<ILeagueRepository>();
        repo.GetNflScoresAsync(2025, 1).Returns([
            new NflScores { Id = 1, Season = 2025, NflWeek = 1, HomeTeam = "BUF", AwayTeam = "MIA",
                HomeTeamScore = 24, AwayTeamScore = 17, GameTime = DateTimeOffset.UtcNow }
        ]);

        var result = await BuildController(repo).GetScores(2025, 1);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsType<List<NflScoreDto>>(ok.Value);
        Assert.Single(dtos);
        Assert.Equal("BUF", dtos[0].HomeTeam);
        Assert.Equal("MIA", dtos[0].AwayTeam);
        Assert.Equal(24, dtos[0].HomeTeamScore);
        Assert.Equal(17, dtos[0].AwayTeamScore);
        Assert.Equal(2025, dtos[0].Season);
        Assert.Equal(1, dtos[0].NflWeek);
    }

    [Fact]
    public async Task GetScoresForSeason_ReturnsNflScoreDtoList_NotRawEntity()
    {
        var repo = Substitute.For<ILeagueRepository>();
        repo.GetAllNflScoresForSeasonAsync(2025).Returns([
            new NflScores { Id = 2, Season = 2025, NflWeek = 2, HomeTeam = "KC", AwayTeam = "DEN",
                HomeTeamScore = 31, AwayTeamScore = 14, GameTime = DateTimeOffset.UtcNow }
        ]);

        var result = await BuildController(repo).GetScoresForSeason(2025);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsType<List<NflScoreDto>>(ok.Value);
        Assert.Single(dtos);
        Assert.Equal("KC", dtos[0].HomeTeam);
        Assert.Equal(2, dtos[0].NflWeek);
    }

    // ── NflSpreadDto ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSpreads_ReturnsNflSpreadDtoList_NotRawEntity()
    {
        var repo = Substitute.For<ILeagueRepository>();
        repo.GetNflSpreadsAsync(2025, 1).Returns([
            new NflSpreads { Id = 1, Season = 2025, NflWeek = 1, HomeTeam = "BUF", AwayTeam = "MIA",
                HomeTeamSpread = -3.5, AwayTeamSpread = 3.5, OverUnder = 47.5, GameTime = DateTimeOffset.UtcNow }
        ]);

        var result = await BuildController(repo).GetSpreads(2025, 1);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsType<List<NflSpreadDto>>(ok.Value);
        Assert.Single(dtos);
        Assert.Equal("BUF", dtos[0].HomeTeam);
        Assert.Equal(-3.5, dtos[0].HomeTeamSpread);
        Assert.Equal(47.5, dtos[0].OverUnder);
    }

    [Fact]
    public async Task GetSpreadsForSeason_ReturnsNflSpreadDtoList_NotRawEntity()
    {
        var repo = Substitute.For<ILeagueRepository>();
        repo.GetAllNflSpreadsForSeasonAsync(2025).Returns([
            new NflSpreads { Id = 2, Season = 2025, NflWeek = 3, HomeTeam = "SF", AwayTeam = "LAR",
                HomeTeamSpread = -6.5, AwayTeamSpread = 6.5, OverUnder = 44.0, GameTime = DateTimeOffset.UtcNow }
        ]);

        var result = await BuildController(repo).GetSpreadsForSeason(2025);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsType<List<NflSpreadDto>>(ok.Value);
        Assert.Single(dtos);
        Assert.Equal("SF", dtos[0].HomeTeam);
        Assert.Equal(3, dtos[0].NflWeek);
        Assert.Equal(44.0, dtos[0].OverUnder);
    }

    [Fact]
    public async Task GetSpreads_WhenNullFromRepo_ReturnsEmptyDtoList()
    {
        var repo = Substitute.For<ILeagueRepository>();
        repo.GetNflSpreadsAsync(2025, 1).Returns((List<NflSpreads>?)null);

        var result = await BuildController(repo).GetSpreads(2025, 1);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsType<List<NflSpreadDto>>(ok.Value);
        Assert.Empty(dtos);
    }
}
