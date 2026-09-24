using System.Security.Claims;
using FourPlayWebApp.Server.Controllers;
using FourPlayWebApp.Server.Jobs;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services;
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

// The spread calculator's league-settings cache must be keyed by (league, season) and dropped when
// a commissioner edits those settings — otherwise NFL spreads keep showing a stale tease.
public class LeagueSettingsCacheTests {
    private const string OwnerId = "owner-1";

    private static List<NflSpreads> Week1(int season) => [
        new() { Season = season, NflWeek = 1, HomeTeam = "KC", AwayTeam = "BAL", HomeTeamSpread = -3, AwayTeamSpread = 3, OverUnder = 45 },
    ];

    [Fact]
    public async Task SpreadCalculatorBuilder_UsesEachSeasonsOwnJuice_WhenTheCacheIsShared() {
        var repo = Substitute.For<ILeagueRepository>();
        repo.GetNflSpreadsAsync(2024, 1).Returns(Week1(2024));
        repo.GetNflSpreadsAsync(2025, 1).Returns(Week1(2025));
        repo.GetLeagueJuiceMappingAsync(1, 2024).Returns(new LeagueJuiceMapping { LeagueId = 1, Season = 2024, Juice = 13 });
        repo.GetLeagueJuiceMappingAsync(1, 2025).Returns(new LeagueJuiceMapping { LeagueId = 1, Season = 2025, Juice = 7 });
        var cache = new MemoryCache(new MemoryCacheOptions());

        var past = await new SpreadCalculatorBuilder(repo, cache).WithLeagueId(1).WithSeason(2024).WithWeek(1).BuildAsync();
        var current = await new SpreadCalculatorBuilder(repo, cache).WithLeagueId(1).WithSeason(2025).WithWeek(1).BuildAsync();

        Assert.Equal((double?)(-3 + 13), past.GetSpread("KC"));
        Assert.Equal((double?)(-3 + 7), current.GetSpread("KC"));
    }

    [Fact]
    public async Task UpdateLeagueJuice_NextSpreadCalculatorUsesTheNewTease_AndLeaderboardCacheIsDropped() {
        var juice = 13;
        var repo = Substitute.For<ILeagueRepository>();
        repo.GetLeagueInfoAsync(1).Returns(new LeagueInfo { Id = 1, OwnerUserId = OwnerId, LeagueName = "L" });
        repo.GetNflSpreadsAsync(2025, 1).Returns(Week1(2025));
        // Fresh instance per call — the controller mutates the one it loads, and a shared instance
        // would make the cached copy "update" by aliasing rather than by invalidation.
        repo.GetLeagueJuiceMappingAsync(1, 2025).Returns(_ => new LeagueJuiceMapping {
            Id = 5, LeagueId = 1, Season = 2025, Juice = juice, JuiceDivisional = 10, JuiceConference = 6, WeeklyCost = 5,
        });
        repo.When(r => r.UpdateLeagueJuiceMappingAsync(Arg.Any<LeagueJuiceMapping>()))
            .Do(ci => juice = ci.Arg<LeagueJuiceMapping>().Juice);
        repo.GetNflSeasonWeekConfigsAsync(2025).Returns(new List<NflSeasonWeekConfig>());

        var cache = new MemoryCache(new MemoryCacheOptions());
        var before = await new SpreadCalculatorBuilder(repo, cache).WithLeagueId(1).WithSeason(2025).WithWeek(1).BuildAsync();
        Assert.Equal((double?)(-3 + 13), before.GetSpread("KC"));
        cache.Set(LeagueCacheKeys.Leaderboard(1, 2025), new List<LeaderboardModel>());

        var ctrl = BuildController(cache, repo);
        var result = await ctrl.UpdateLeagueJuice(1, 2025, new LeagueJuiceUpdateDto(9, 10, 6, 5),
            new LeagueJuiceScheduleSource(repo, Substitute.For<ICfbRepository>(), TimeProvider.System));

        Assert.IsType<NoContentResult>(result);
        var after = await new SpreadCalculatorBuilder(repo, cache).WithLeagueId(1).WithSeason(2025).WithWeek(1).BuildAsync();
        Assert.Equal((double?)(-3 + 9), after.GetSpread("KC"));
        Assert.False(cache.TryGetValue(LeagueCacheKeys.Leaderboard(1, 2025), out _));
    }

    [Fact]
    public async Task RollForwardJuice_DropsTheTargetSeasonsCachedSettings() {
        var repo = Substitute.For<ILeagueRepository>();
        repo.GetLeagueInfoAsync(1).Returns(new LeagueInfo { Id = 1, OwnerUserId = OwnerId, LeagueName = "L" });
        repo.GetLeagueJuiceMappingAsync(1, 2026).Returns((LeagueJuiceMapping?)null);
        repo.GetLeagueJuiceMappingAsync(1).Returns([new LeagueJuiceMapping { LeagueId = 1, Season = 2025, Juice = 13, WeeklyCost = 5 }]);
        var cache = new MemoryCache(new MemoryCacheOptions());
        // A calculator built while the season had no mapping yet caches the "not configured" state.
        cache.Set(LeagueCacheKeys.Juice(1, 2026), new LeagueJuiceMapping());
        cache.Set(LeagueCacheKeys.Leaderboard(1, 2026), new List<LeaderboardModel>());

        var result = await BuildController(cache, repo).RollForwardJuice(1, 2026);

        Assert.IsType<NoContentResult>(result);
        Assert.False(cache.TryGetValue(LeagueCacheKeys.Juice(1, 2026), out _));
        Assert.False(cache.TryGetValue(LeagueCacheKeys.Leaderboard(1, 2026), out _));
    }

    private static LeagueController BuildController(IMemoryCache cache, ILeagueRepository repo) {
        var userManager = Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);
        var ctrl = new LeagueController(
            cache, repo, NullLogger<LeagueController>.Instance, userManager,
            Substitute.For<ISpreadCalculatorBuilder>(), Substitute.For<IEspnCacheService>(),
            Substitute.For<IInvitationService>(), Substitute.For<ILeagueInviteLinkService>(),
            Substitute.For<ILeagueMembershipInviteService>());
        ctrl.ControllerContext = new ControllerContext {
            HttpContext = new DefaultHttpContext {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, OwnerId)], "test")),
            },
        };
        return ctrl;
    }
}
