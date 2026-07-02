using FourPlayWebApp.Server.Controllers;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using System.Security.Claims;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// Smoke tests for EspnController — verifies that each endpoint:
///   - returns 200 OK regardless of cache state (fail-open design)
///   - delegates to the correct service
/// </summary>
public class EspnControllerTests
{
    private readonly IEspnApiService _espnApiService;
    private readonly IEspnCacheService _espnCacheService;
    private readonly EspnController _sut;

    public EspnControllerTests()
    {
        _espnApiService  = Substitute.For<IEspnApiService>();
        _espnCacheService = Substitute.For<IEspnCacheService>();

        _sut = new EspnController(_espnApiService, _espnCacheService);
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, "user-1")], "Test"))
            }
        };
    }

    // ── GetScores (cached NFL scores) ────────────────────────────────────────

    [Fact]
    public async Task GetScores_ReturnsOk_WithCachedScores()
    {
        var scores = new EspnScores { Season = new Season { Year = 2025 } };
        _espnCacheService.GetScoresAsync().Returns(scores);

        var result = await _sut.GetScores();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(scores, ok.Value);
    }

    [Fact]
    public async Task GetScores_ReturnsOk_WhenCacheIsNull()
    {
        _espnCacheService.GetScoresAsync().Returns((EspnScores?)null);

        var result = await _sut.GetScores();

        // Fail-open: returns empty EspnScores, not 404
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<EspnScores>(ok.Value);
    }

    // ── GetWeekScores (proxied from IEspnApiService) ─────────────────────────

    [Fact]
    public async Task GetWeekScores_ReturnsOk_WithScores()
    {
        var scores = new EspnScores { Season = new Season { Year = 2025 } };
        _espnApiService.GetWeekScores(10, 2025, false).Returns(scores);

        var result = await _sut.GetWeekScores(10, 2025);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(scores, ok.Value);
    }

    [Fact]
    public async Task GetWeekScores_ReturnsOk_WhenServiceReturnsNull()
    {
        _espnApiService.GetWeekScores(1, 2025, false).Returns((EspnScores?)null);

        var result = await _sut.GetWeekScores(1, 2025);

        // Fail-open: returns empty EspnScores
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<EspnScores>(ok.Value);
    }

    [Fact]
    public async Task GetWeekScores_PassesPostSeasonFlag_ToService()
    {
        _espnApiService.GetWeekScores(1, 2025, true).Returns(new EspnScores());

        await _sut.GetWeekScores(1, 2025, postSeason: true);

        await _espnApiService.Received(1).GetWeekScores(1, 2025, true);
    }

    // ── GetCfbScores ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCfbScores_ReturnsOk_WithScores()
    {
        var start = new DateOnly(2025, 9, 1);
        var end   = new DateOnly(2025, 9, 7);
        var scores = new EspnScores();
        _espnApiService.GetCfbScores(start, end).Returns(scores);

        var result = await _sut.GetCfbScores(start, end);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(scores, ok.Value);
    }

    [Fact]
    public async Task GetCfbScores_ReturnsOk_WhenServiceReturnsNull()
    {
        var start = new DateOnly(2025, 9, 1);
        var end   = new DateOnly(2025, 9, 7);
        _espnApiService.GetCfbScores(start, end).Returns((EspnScores?)null);

        var result = await _sut.GetCfbScores(start, end);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<EspnScores>(ok.Value);
    }

    // ── GetLiveGames ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetLiveGames_ReturnsOk_EmptyList_WhenCacheIsNull()
    {
        _espnCacheService.GetScoresAsync().Returns((EspnScores?)null);

        var result = await _sut.GetLiveGames();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsType<System.Collections.Generic.List<FourPlayWebApp.Shared.Models.Data.Dtos.LiveGameDto>>(ok.Value);
        Assert.Empty(list);
    }

    [Fact]
    public async Task GetLiveGames_ReturnsOk_EmptyList_WhenEventsIsNull()
    {
        _espnCacheService.GetScoresAsync().Returns(new EspnScores { Events = null });

        var result = await _sut.GetLiveGames();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsType<System.Collections.Generic.List<FourPlayWebApp.Shared.Models.Data.Dtos.LiveGameDto>>(ok.Value);
        Assert.Empty(list);
    }
}
