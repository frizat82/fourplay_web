using FourPlayWebApp.Server.Controllers;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models.Data;
using NSubstitute;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FourPlayWebApp.Server.UnitTests;

public class CfbPicksControllerTests
{
    private readonly ICfbPicksRepository _repo;
    private readonly ICfbRepository _cfbRepo;
    private const string UserId = "user-123";

    public CfbPicksControllerTests()
    {
        _repo = Substitute.For<ICfbPicksRepository>();
        _cfbRepo = Substitute.For<ICfbRepository>();
    }

    private CfbPicksController BuildController(string userId = UserId)
    {
        var ctrl = new CfbPicksController(_repo, _cfbRepo);
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, userId)
                ], "Test"))
            }
        };
        return ctrl;
    }

    [Fact]
    public async Task GetUserPicks_ReturnsPicksForUser()
    {
        var picks = new List<CfbPicks>
        {
            new() { Id = 1, UserId = UserId, LeagueId = 1, CfbSlateId = 1, Team = "ORE", EspnEventId = 401800001 }
        };
        _repo.GetUserPicksAsync(1, 1, UserId).Returns(picks);

        var result = await BuildController().GetUserPicks(1, 1);

        var ok = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsAssignableFrom<IEnumerable<CfbPicks>>(ok.Value);
        Assert.Single(returned);
    }

    [Fact]
    public async Task AddPicks_ValidPicks_ReturnsCount()
    {
        var request = new AddCfbPicksRequest
        {
            LeagueId = 1,
            CfbSlateId = 1,
            Season = 2025,
            Picks = [new CfbPickItem { Team = "ORE", EspnEventId = 401800001, PickType = "Spread" }]
        };
        _repo.GetUserPicksAsync(1, 1, UserId).Returns([]);
        _repo.AddPicksAsync(Arg.Any<IEnumerable<CfbPicks>>()).Returns(Task.CompletedTask);

        var result = await BuildController().AddPicks(request);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public async Task AddPicks_DuplicatePick_IsSkipped()
    {
        var existing = new List<CfbPicks>
        {
            new() { UserId = UserId, LeagueId = 1, CfbSlateId = 1, Team = "ORE", EspnEventId = 401800001 }
        };
        _repo.GetUserPicksAsync(1, 1, UserId).Returns(existing);

        var request = new AddCfbPicksRequest
        {
            LeagueId = 1, CfbSlateId = 1, Season = 2025,
            Picks = [new CfbPickItem { Team = "ORE", EspnEventId = 401800001, PickType = "Spread" }]
        };

        var result = await BuildController().AddPicks(request);

        await _repo.DidNotReceive().AddPicksAsync(Arg.Any<IEnumerable<CfbPicks>>());
        Assert.IsType<OkObjectResult>(result);
    }
}
