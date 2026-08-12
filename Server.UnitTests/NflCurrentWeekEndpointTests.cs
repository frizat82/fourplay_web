using FourPlayWebApp.Server.Controllers;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// GET /api/league/current-week exposes NflCurrentWeekService (already used internally by
/// NflSpreadJob/EspnCacheService, with a real off-season/pre-season fallback) to the frontend —
/// mirrors CfbPicksController.GetCurrentSlate's shape/role for CFB. Fixes nflAdapter.ts falling
/// back to `new Date().getFullYear()` (today's real calendar year, not a season with any data)
/// whenever ESPN's live "current" scoreboard has nothing in progress.
/// </summary>
public class NflCurrentWeekEndpointTests
{
    private static LeagueController BuildController(INflCurrentWeekService svc)
    {
        var repo = Substitute.For<ILeagueRepository>();
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
            Substitute.For<IInvitationService>());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = TestPrincipalFactory.Build("user-1") }
        };
        return controller;
    }

    [Fact]
    public async Task GetCurrentWeek_ReturnsWeekInfo_FromNflCurrentWeekService()
    {
        var svc = Substitute.For<INflCurrentWeekService>();
        var info = new NflWeekInfo(6, 6, 2026, false, "Week 6", "Standard", new DateTime(2026, 10, 8, 18, 0, 0, DateTimeKind.Utc));
        svc.GetCurrentWeekAsync().Returns(info);

        var result = await BuildController(svc).GetCurrentWeek(svc);

        var ok = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsType<NflWeekInfo>(ok.Value);
        Assert.Equal(2026, returned.Season);
        Assert.Equal(6, returned.WeekId);
        Assert.False(returned.IsPostSeason);
    }

    [Fact]
    public async Task GetCurrentWeek_ReturnsOffSeasonFallback_NotCallersConcern()
    {
        // NflCurrentWeekService already resolves off-season/pre-season fallbacks internally
        // (most recent completed week, or upcoming Week 1) — the controller just passes it through
        // unconditionally rather than re-implementing any of that logic.
        var svc = Substitute.For<INflCurrentWeekService>();
        var info = new NflWeekInfo(18, 18, 2025, false, "Week 18", "Standard", new DateTime(2026, 1, 4, 18, 0, 0, DateTimeKind.Utc));
        svc.GetCurrentWeekAsync().Returns(info);

        var result = await BuildController(svc).GetCurrentWeek(svc);

        var ok = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsType<NflWeekInfo>(ok.Value);
        Assert.Equal(2025, returned.Season);
        Assert.Equal(18, returned.WeekId);
    }
}
