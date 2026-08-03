using FourPlayWebApp.Server.Controllers;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Data.Dtos;
using NSubstitute;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FourPlayWebApp.Server.UnitTests;

public class CfbPicksControllerTests
{
    private readonly ICfbPicksRepository _repo;
    private readonly ICfbRepository _cfbRepo;
    private readonly ILeagueRepository _leagueRepo;
    private const string UserId = "user-123";
    private const string OtherUserId = "other-user-456";

    public CfbPicksControllerTests()
    {
        _repo = Substitute.For<ICfbPicksRepository>();
        _cfbRepo = Substitute.For<ICfbRepository>();
        _leagueRepo = Substitute.For<ILeagueRepository>();
        // Default: caller is a member of league 1 — individual tests override to test the guard.
        _leagueRepo.UserExistsInLeagueAsync(Arg.Any<string>(), 1).Returns(true);
    }

    private CfbPicksController BuildController(string userId = UserId, bool isAdmin = false)
    {
        var ctrl = new CfbPicksController(_repo, _cfbRepo, _leagueRepo);
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId) };
        if (isAdmin) claims.Add(new Claim(ClaimTypes.Role, "Administrator"));
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
            }
        };
        return ctrl;
    }

    private static CfbSlates MakeSlate(int id = 1, int slateNumber = 1) => new()
    {
        Id = id, Season = 2025, SlateNumber = slateNumber, Label = "Week 1", SlateType = "RegularSeason",
    };

    private static CfbSpreads MakeSpread(int espnEventId, DateTimeOffset gameTime, string home = "ORE", string away = "OSU") => new()
    {
        CfbSlateId = 1, EspnEventId = espnEventId, HomeTeam = home, AwayTeam = away, GameTime = gameTime,
    };

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
        _cfbRepo.GetSlateByIdAsync(1).Returns(MakeSlate());
        _cfbRepo.GetSpreadsForSlateAsync(1).Returns([MakeSpread(401800001, DateTimeOffset.UtcNow.AddHours(2))]);
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
        _cfbRepo.GetSlateByIdAsync(1).Returns(MakeSlate());
        _cfbRepo.GetSpreadsForSlateAsync(1).Returns([MakeSpread(401800001, DateTimeOffset.UtcNow.AddHours(2))]);
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

    // ── AddPicks — league membership guard ──────────────────────────────────

    [Fact]
    public async Task AddPicks_ReturnsForbid_WhenUserNotInLeague()
    {
        _leagueRepo.UserExistsInLeagueAsync(UserId, 1).Returns(false);
        var request = new AddCfbPicksRequest
        {
            LeagueId = 1, CfbSlateId = 1, Season = 2025,
            Picks = [new CfbPickItem { Team = "ORE", EspnEventId = 401800001, PickType = "Spread" }]
        };

        var result = await BuildController().AddPicks(request);

        Assert.IsType<ForbidResult>(result);
        await _repo.DidNotReceive().AddPicksAsync(Arg.Any<IEnumerable<CfbPicks>>());
    }

    [Fact]
    public async Task AddPicks_WhenSlateDoesNotExist_ReturnsBadRequest()
    {
        // Regression: the required-pick-count cap must fail closed on a bad/stale CfbSlateId, not
        // silently skip enforcement — GetSlateByIdAsync returning null (default NSubstitute stub)
        // must reject the whole request before any picks are inserted.
        _cfbRepo.GetSpreadsForSlateAsync(1).Returns([MakeSpread(401800001, DateTimeOffset.UtcNow.AddHours(2))]);
        _repo.GetUserPicksAsync(1, 1, UserId).Returns([]);
        var request = new AddCfbPicksRequest
        {
            LeagueId = 1, CfbSlateId = 1, Season = 2025,
            Picks = [new CfbPickItem { Team = "ORE", EspnEventId = 401800001, PickType = "Spread" }]
        };

        var result = await BuildController().AddPicks(request);

        Assert.IsType<BadRequestObjectResult>(result);
        await _repo.DidNotReceive().AddPicksAsync(Arg.Any<IEnumerable<CfbPicks>>());
    }

    // ── AddPicks — kickoff guard ─────────────────────────────────────────────

    [Fact]
    public async Task AddPicks_WhenGameKickoffHasPassed_ReturnsBadRequest()
    {
        _cfbRepo.GetSlateByIdAsync(1).Returns(MakeSlate());
        _cfbRepo.GetSpreadsForSlateAsync(1).Returns([MakeSpread(401800001, DateTimeOffset.UtcNow.AddHours(-2))]);
        _repo.GetUserPicksAsync(1, 1, UserId).Returns([]);
        var request = new AddCfbPicksRequest
        {
            LeagueId = 1, CfbSlateId = 1, Season = 2025,
            Picks = [new CfbPickItem { Team = "ORE", EspnEventId = 401800001, PickType = "Spread" }]
        };

        var result = await BuildController().AddPicks(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("kicked off", badRequest.Value?.ToString(), StringComparison.OrdinalIgnoreCase);
        await _repo.DidNotReceive().AddPicksAsync(Arg.Any<IEnumerable<CfbPicks>>());
    }

    [Fact]
    public async Task AddPicks_WhenGameKickoffIsInFuture_ReturnsOk()
    {
        _cfbRepo.GetSlateByIdAsync(1).Returns(MakeSlate());
        _cfbRepo.GetSpreadsForSlateAsync(1).Returns([MakeSpread(401800001, DateTimeOffset.UtcNow.AddHours(2))]);
        _repo.GetUserPicksAsync(1, 1, UserId).Returns([]);
        _repo.AddPicksAsync(Arg.Any<IEnumerable<CfbPicks>>()).Returns(Task.CompletedTask);
        var request = new AddCfbPicksRequest
        {
            LeagueId = 1, CfbSlateId = 1, Season = 2025,
            Picks = [new CfbPickItem { Team = "ORE", EspnEventId = 401800001, PickType = "Spread" }]
        };

        var result = await BuildController().AddPicks(request);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task AddPicks_WhenNoMatchingSpread_AllowsPick()
    {
        // Fail open — same rule as NFL when ESPN cache is unavailable/has no match for the team.
        _cfbRepo.GetSlateByIdAsync(1).Returns(MakeSlate());
        _cfbRepo.GetSpreadsForSlateAsync(1).Returns([]);
        _repo.GetUserPicksAsync(1, 1, UserId).Returns([]);
        _repo.AddPicksAsync(Arg.Any<IEnumerable<CfbPicks>>()).Returns(Task.CompletedTask);
        var request = new AddCfbPicksRequest
        {
            LeagueId = 1, CfbSlateId = 1, Season = 2025,
            Picks = [new CfbPickItem { Team = "ORE", EspnEventId = 401800001, PickType = "Spread" }]
        };

        var result = await BuildController().AddPicks(request);

        Assert.IsType<OkObjectResult>(result);
    }

    // ── AddPicks — required-pick-count cap ──────────────────────────────────

    [Fact]
    public async Task AddPicks_TooManyPicks_ReturnsBadRequest()
    {
        // Slate 18 (Championship-style, > 17) requires exactly 1 pick.
        _cfbRepo.GetSlateByIdAsync(1).Returns(MakeSlate(slateNumber: 18));
        _cfbRepo.GetSpreadsForSlateAsync(1).Returns([
            MakeSpread(401800001, DateTimeOffset.UtcNow.AddHours(2), "ORE", "OSU"),
            MakeSpread(401800002, DateTimeOffset.UtcNow.AddHours(2), "ALA", "UGA"),
        ]);
        _repo.GetUserPicksAsync(1, 1, UserId).Returns([]);
        var request = new AddCfbPicksRequest
        {
            LeagueId = 1, CfbSlateId = 1, Season = 2025,
            Picks =
            [
                new CfbPickItem { Team = "ORE", EspnEventId = 401800001, PickType = "Spread" },
                new CfbPickItem { Team = "ALA", EspnEventId = 401800002, PickType = "Spread" },
            ]
        };

        var result = await BuildController().AddPicks(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Too many picks", badRequest.Value?.ToString(), StringComparison.OrdinalIgnoreCase);
        await _repo.DidNotReceive().AddPicksAsync(Arg.Any<IEnumerable<CfbPicks>>());
    }

    // ── GetAllPicks — membership + pick-reveal gate ─────────────────────────

    [Fact]
    public async Task GetAllPicks_ReturnsForbid_WhenUserNotInLeague()
    {
        _leagueRepo.UserExistsInLeagueAsync(UserId, 1).Returns(false);

        var result = await BuildController().GetAllPicks(1, 1);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task GetAllPicks_HidesOtherUsersPicksForNotYetStartedGames()
    {
        _cfbRepo.GetSpreadsForSlateAsync(1).Returns([MakeSpread(401800001, DateTimeOffset.UtcNow.AddHours(2))]);
        _repo.GetAllPicksForSlateAsync(1, 1).Returns(
        [
            new CfbPickDto { UserId = UserId, EspnEventId = 401800001, Team = "ORE" },
            new CfbPickDto { UserId = OtherUserId, EspnEventId = 401800001, Team = "OSU" },
        ]);

        var result = await BuildController().GetAllPicks(1, 1);

        var ok = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsAssignableFrom<IEnumerable<CfbPickDto>>(ok.Value).ToList();
        Assert.Single(returned);
        Assert.Equal(UserId, returned[0].UserId);
    }

    [Fact]
    public async Task GetAllPicks_ShowsOtherUsersPicksForStartedGames()
    {
        _cfbRepo.GetSpreadsForSlateAsync(1).Returns([MakeSpread(401800001, DateTimeOffset.UtcNow.AddHours(-1))]);
        _repo.GetAllPicksForSlateAsync(1, 1).Returns(
        [
            new CfbPickDto { UserId = UserId, EspnEventId = 401800001, Team = "ORE" },
            new CfbPickDto { UserId = OtherUserId, EspnEventId = 401800001, Team = "OSU" },
        ]);

        var result = await BuildController().GetAllPicks(1, 1);

        var ok = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsAssignableFrom<IEnumerable<CfbPickDto>>(ok.Value).ToList();
        Assert.Equal(2, returned.Count);
    }

    [Fact]
    public async Task GetAllPicks_AdminSeesAllPicksRegardlessOfGameStatus()
    {
        _leagueRepo.UserExistsInLeagueAsync(Arg.Any<string>(), 1).Returns(false); // admin not a member
        _cfbRepo.GetSpreadsForSlateAsync(1).Returns([MakeSpread(401800001, DateTimeOffset.UtcNow.AddHours(2))]);
        _repo.GetAllPicksForSlateAsync(1, 1).Returns(
        [
            new CfbPickDto { UserId = UserId, EspnEventId = 401800001, Team = "ORE" },
            new CfbPickDto { UserId = OtherUserId, EspnEventId = 401800001, Team = "OSU" },
        ]);

        var result = await BuildController("admin-001", isAdmin: true).GetAllPicks(1, 1);

        var ok = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsAssignableFrom<IEnumerable<CfbPickDto>>(ok.Value).ToList();
        Assert.Equal(2, returned.Count);
    }

    // ── DeletePicks — admin-only ─────────────────────────────────────────────

    [Fact]
    public void DeletePicks_IsRestrictedToAdministratorRole()
    {
        var method = typeof(CfbPicksController).GetMethod(nameof(CfbPicksController.DeletePicks));
        var attr = method!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(attr);
        Assert.Equal("Administrator", attr!.Roles);
    }
}
