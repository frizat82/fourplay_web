using System.Security.Claims;
using FourPlayWebApp.Server.Controllers;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FourPlayWebApp.Server.UnitTests;

// The NFL Picks/Scores pages used to wait for ESPN's scores just to learn which teams to ask for —
// an empty request now means "every team with odds this week", so the frontend can fetch spreads
// in parallel with scores (and derive hasOdds from the same call: 404 = no odds).
public class SpreadBatchTests {
    private const string UserId = "member-1";

    private static LeagueController BuildController() {
        var repo = Substitute.For<ILeagueRepository>();
        repo.UserExistsInLeagueAsync(UserId, 1).Returns(true);
        repo.GetNflSpreadsAsync(2025, 3).Returns(new List<NflSpreads> {
            new() { Season = 2025, NflWeek = 3, HomeTeam = "KC", AwayTeam = "BAL", HomeTeamSpread = -3, AwayTeamSpread = 3, OverUnder = 45 },
            new() { Season = 2025, NflWeek = 3, HomeTeam = "BUF", AwayTeam = "MIA", HomeTeamSpread = -7, AwayTeamSpread = 7, OverUnder = 48 },
        });
        repo.GetLeagueJuiceMappingAsync(1, 2025).Returns(new LeagueJuiceMapping { LeagueId = 1, Season = 2025, Juice = 10 });
        var cache = new MemoryCache(new MemoryCacheOptions());

        var userManager = Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);
        var ctrl = new LeagueController(
            cache, repo, NullLogger<LeagueController>.Instance, userManager,
            new SpreadCalculatorBuilder(repo, cache), Substitute.For<IEspnCacheService>(),
            Substitute.For<IInvitationService>(), Substitute.For<ILeagueInviteLinkService>(),
            Substitute.For<ILeagueMembershipInviteService>());
        ctrl.ControllerContext = new ControllerContext {
            HttpContext = new DefaultHttpContext {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, UserId)], "test")),
            },
        };
        return ctrl;
    }

    [Fact]
    public async Task GetSpreadBatch_WithNoTeamsRequested_ReturnsEveryTeamWithOddsThisWeek() {
        var result = await BuildController().GetSpreadBatch(1, 2025, 3, new BatchSpreadRequest());

        var body = Assert.IsType<BatchSpreadResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(["BAL", "BUF", "KC", "MIA"], body.Responses.Keys.Order());
        Assert.Equal((double?)(-3 + 10), body.Responses["KC"].Spread);
        Assert.Equal((double?)(7 + 10), body.Responses["MIA"].Spread);
    }

    [Fact]
    public async Task GetSpreadBatch_WithTeamsRequested_ReturnsOnlyThoseTeams() {
        var request = new BatchSpreadRequest { Requests = [new SpreadRequest { Team = "KC" }] };

        var result = await BuildController().GetSpreadBatch(1, 2025, 3, request);

        var body = Assert.IsType<BatchSpreadResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(["KC"], body.Responses.Keys);
    }
}
