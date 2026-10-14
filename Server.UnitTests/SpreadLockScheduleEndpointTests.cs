using FourPlayWebApp.Server.Controllers;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models.Data.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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
        var userManager = UserManagerStub.Create();

        var controller = LeagueControllerFactory.Build(repo, userManager);

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
        svc.GetCurrentWeekAsync().Returns(new NflWeekInfo(3, 2026, false, "Week 3", "Standard", DateTime.UtcNow, 4));
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

    // frizat-d47: the Rules page reads each round's pick count and postseason flag off these rows.
    [Fact]
    public async Task Nfl_GetSpreadLockSchedule_CarriesEachRowsPostSeasonFlagAndPickCount()
    {
        var repo = Substitute.For<ILeagueRepository>();
        var svc = Substitute.For<INflCurrentWeekService>();
        svc.GetCurrentWeekAsync().Returns(new NflWeekInfo(3, 2026, false, "Week 3", "Standard", DateTime.UtcNow, 4));
        repo.GetNflSeasonWeekConfigsAsync().Returns([
            new NflSeasonWeekConfig { Season = 2026, WeekId = 19, WeekLabel = "Wild Card Weekend", IsPostSeason = true, RequiredPicks = 3 },
        ]);

        var result = await BuildLeagueController(repo).GetSpreadLockSchedule(svc);

        var week = Assert.Single(Assert.IsAssignableFrom<IEnumerable<SpreadLockWeekDto>>(Assert.IsType<OkObjectResult>(result).Value));
        Assert.True(week.IsPostSeason);
        Assert.Equal(3, week.RequiredPicks);
    }

    // frizat-d47: past NFL weeks get their pick count from that season's rows (nflAdapter).
    [Fact]
    public async Task Nfl_GetSeasonWeeks_ReturnsThatSeasonsRows_OrderedByWeek_WithTheirRules()
    {
        var repo = Substitute.For<ILeagueRepository>();
        repo.GetNflSeasonWeekConfigsAsync(2025).Returns([
            new NflSeasonWeekConfig { Season = 2025, WeekId = 22, WeekLabel = "Super Bowl LX", IsPostSeason = true, RequiredPicks = 1 },
            new NflSeasonWeekConfig { Season = 2025, WeekId = 1, WeekLabel = "Week 1", IsPostSeason = false, RequiredPicks = 4 },
        ]);

        var result = await BuildLeagueController(repo).GetSeasonWeeks(2025);

        var weeks = Assert.IsAssignableFrom<IEnumerable<NflSeasonWeekDto>>(Assert.IsType<OkObjectResult>(result).Value).ToList();
        Assert.Equal([new NflSeasonWeekDto(1, "Week 1", false, 4), new NflSeasonWeekDto(22, "Super Bowl LX", true, 1)], weeks);
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
    public async Task Cfb_GetSpreadLockSchedule_CarriesEachRowsPostSeasonFlagAndPickCount()
    {
        var cfbRepo = Substitute.For<ICfbRepository>();
        var svc = Substitute.For<ICfbCurrentSlateService>();
        svc.GetCurrentSlateAsync().Returns(new CfbSlateInfo(
            1, 2026, 1, "Week 1", "RegularSeason", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 7), null, DateTime.UtcNow, false, 4));
        var semis = MakeConfig(17, "FBS Playoff", new DateTime(2027, 1, 8, 12, 0, 0, DateTimeKind.Utc));
        semis.ScoringFormat = "NFLConference";
        semis.IsPostSeason = true;
        semis.RequiredPicks = 2;
        cfbRepo.GetWeekConfigsForSeasonAsync(2026).Returns([semis]);

        var result = await BuildCfbController(cfbRepo).GetSpreadLockSchedule(svc);

        var week = Assert.Single(Assert.IsAssignableFrom<IEnumerable<SpreadLockWeekDto>>(Assert.IsType<OkObjectResult>(result).Value));
        Assert.True(week.IsPostSeason);
        Assert.Equal(2, week.RequiredPicks);
    }

    [Fact]
    public async Task Cfb_GetSpreadLockSchedule_ReturnsOnlyInScopeWeeks_OrderedByWeek()
    {
        var cfbRepo = Substitute.For<ICfbRepository>();
        var svc = Substitute.For<ICfbCurrentSlateService>();
        svc.GetCurrentSlateAsync().Returns(new CfbSlateInfo(
            1, 2026, 1, "Week 1", "RegularSeason", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 7), null, DateTime.UtcNow, false, 4));
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
