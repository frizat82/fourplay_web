using FourPlayWebApp.Server.Controllers;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models.Data.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// Rules page needs the full season's spread-lock schedule, sport-scoped, for every logged-in
/// user (not just admins) -- both endpoints resolve "current season" the same way the existing
/// current-week/current-slate endpoints do, then return every in-scope week's lock time for that
/// season, so the frontend doesn't need a separate round-trip just to learn which season to ask
/// for.
/// </summary>
public class SpreadLockScheduleEndpointTests
{
    // ── NFL: LeagueController.GetSpreadLockSchedule ─────────────────────────

    private static LeagueController BuildLeagueController(ILeagueRepository repo)
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
            Substitute.For<IInvitationService>());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = TestPrincipalFactory.Build("user-1") }
        };
        return controller;
    }

    [Fact]
    public async Task Nfl_GetSpreadLockSchedule_ReturnsOnlyCurrentSeasonsWeeks_OrderedByWeek()
    {
        var repo = Substitute.For<ILeagueRepository>();
        var svc = Substitute.For<INflCurrentWeekService>();
        svc.GetCurrentWeekAsync().Returns(new NflWeekInfo(3, 3, 2026, false, "Week 3", "Standard", DateTime.UtcNow));
        repo.GetNflSeasonWeekConfigsAsync().Returns([
            new NflSeasonWeekConfig { Season = 2026, WeekId = 2, WeekLabel = "Week 2", SpreadLockDatetime = new DateTime(2026, 9, 10, 18, 0, 0, DateTimeKind.Utc) },
            new NflSeasonWeekConfig { Season = 2026, WeekId = 1, WeekLabel = "Week 1", SpreadLockDatetime = new DateTime(2026, 9, 3, 18, 0, 0, DateTimeKind.Utc) },
            new NflSeasonWeekConfig { Season = 2025, WeekId = 1, WeekLabel = "Week 1", SpreadLockDatetime = new DateTime(2025, 9, 4, 18, 0, 0, DateTimeKind.Utc) },
        ]);

        var result = await BuildLeagueController(repo).GetSpreadLockSchedule(svc);

        var ok = Assert.IsType<OkObjectResult>(result);
        var weeks = Assert.IsAssignableFrom<IEnumerable<SpreadLockWeekDto>>(ok.Value).ToList();
        Assert.Equal(2, weeks.Count);
        Assert.Equal("Week 1", weeks[0].WeekLabel);
        Assert.Equal("Week 2", weeks[1].WeekLabel);
    }

    // ── CFB: CfbPicksController.GetSpreadLockSchedule ───────────────────────

    private static CfbPicksController BuildCfbController(ICfbRepository cfbRepo)
    {
        var repo = Substitute.For<ICfbPicksRepository>();
        var leagueRepo = Substitute.For<ILeagueRepository>();
        return new CfbPicksController(repo, cfbRepo, leagueRepo);
    }

    private static CfbSeasonWeekConfig MakeConfig(int ivWeek, string weekType, DateTime lock_, bool inScope = true) => new() {
        Season = 2026, EspnWeekNumber = ivWeek, IvLeagueWeekNumber = ivWeek,
        WeekType = weekType, ScoringFormat = "Standard", InScopeIvLeague = inScope,
        SpreadLockDatetime = lock_,
    };

    [Fact]
    public async Task Cfb_GetSpreadLockSchedule_ReturnsOnlyInScopeWeeks_OrderedByWeek()
    {
        var cfbRepo = Substitute.For<ICfbRepository>();
        var svc = Substitute.For<ICfbCurrentSlateService>();
        svc.GetCurrentSlateAsync().Returns(new CfbSlateInfo(
            1, 2026, 1, "Week 1", "RegularSeason", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 7), null, DateTime.UtcNow));
        cfbRepo.GetWeekConfigsForSeasonAsync(2026).Returns([
            MakeConfig(0, "Regular Season", new DateTime(2026, 8, 27, 12, 0, 0, DateTimeKind.Utc), inScope: false),
            MakeConfig(2, "Regular Season", new DateTime(2026, 9, 10, 13, 0, 0, DateTimeKind.Utc)),
            MakeConfig(1, "Regular Season", new DateTime(2026, 9, 3, 12, 0, 0, DateTimeKind.Utc)),
        ]);

        var result = await BuildCfbController(cfbRepo).GetSpreadLockSchedule(svc);

        var ok = Assert.IsType<OkObjectResult>(result);
        var weeks = Assert.IsAssignableFrom<IEnumerable<SpreadLockWeekDto>>(ok.Value).ToList();
        Assert.Equal(2, weeks.Count);
        Assert.Equal("Week 1", weeks[0].WeekLabel);
        Assert.Equal("Week 2", weeks[1].WeekLabel);
    }

    [Fact]
    public async Task Cfb_GetSpreadLockSchedule_ReturnsEmpty_WhenNoCurrentSlateResolved()
    {
        var cfbRepo = Substitute.For<ICfbRepository>();
        var svc = Substitute.For<ICfbCurrentSlateService>();
        svc.GetCurrentSlateAsync().Returns((CfbSlateInfo?)null);

        var result = await BuildCfbController(cfbRepo).GetSpreadLockSchedule(svc);

        var ok = Assert.IsType<OkObjectResult>(result);
        var weeks = Assert.IsAssignableFrom<IEnumerable<SpreadLockWeekDto>>(ok.Value);
        Assert.Empty(weeks);
    }
}
