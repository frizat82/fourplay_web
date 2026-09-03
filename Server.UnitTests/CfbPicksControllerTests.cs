using FourPlayWebApp.Server.Controllers;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Data.Dtos;
using FourPlayWebApp.Shared.Models.Enum;
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
        // Default: no rankings captured — individual rank tests override.
        _cfbRepo.GetLatestRankingsForWeekAsync(Arg.Any<int>(), Arg.Any<int>()).Returns(new Dictionary<string, int>());
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

    private static CfbSlates MakeSlate(int id = 1, int slateNumber = 1, int? espnWeekNumber = 1) => new()
    {
        Id = id, Season = 2025, SlateNumber = slateNumber, Label = "Week 1", SlateType = "RegularSeason",
        EspnWeekNumber = espnWeekNumber,
    };

    // frizat: a team plays at most one game per slate, so (CfbSlateId, HomeTeam) — not an ESPN id
    // — is what uniquely identifies a game, mirroring NflSpreads' (Season, NflWeek, HomeTeam).
    private static CfbSpreads MakeSpread(DateTimeOffset gameTime, string home = "ORE", string away = "OSU", bool isLeagueEligible = true) => new()
    {
        CfbSlateId = 1, HomeTeam = home, AwayTeam = away, GameTime = gameTime,
        IsLeagueEligible = isLeagueEligible,
    };

    // /code-review + live CI failure: this endpoint used to return the raw CfbPicks entity
    // directly (Ok(picks)) — harmless while PickType was a plain string, but once PickType became
    // an enum, the entity (no JsonStringEnumConverter) silently started serializing it as an int
    // instead of "Spread"/"Over"/"Under", breaking every frontend consumer that matches by that
    // string. Must go through CfbPickDto like every other picks-returning endpoint already does.
    [Fact]
    public async Task GetUserPicks_ReturnsPicksForUser()
    {
        var picks = new List<CfbPicks>
        {
            new() { Id = 1, UserId = UserId, LeagueId = 1, CfbSlateId = 1, Team = "ORE", PickType = PickType.Spread }
        };
        _repo.GetUserPicksAsync(1, 1, UserId).Returns(picks);

        var result = await BuildController().GetUserPicks(1, 1);

        var ok = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsAssignableFrom<IEnumerable<CfbPickDto>>(ok.Value).ToList();
        Assert.Single(returned);
        Assert.Equal(PickType.Spread, returned[0].PickType);
        Assert.Equal("ORE", returned[0].Team);
    }

    [Fact]
    public async Task AddPicks_ValidPicks_ReturnsCount()
    {
        _cfbRepo.GetSlateByIdAsync(1).Returns(MakeSlate());
        _cfbRepo.GetSpreadsForSlateAsync(1).Returns([MakeSpread(DateTimeOffset.UtcNow.AddHours(2))]);
        var request = new AddCfbPicksRequest
        {
            LeagueId = 1,
            CfbSlateId = 1,
            Season = 2025,
            Picks = [new CfbPickItem { Team = "ORE", PickType = PickType.Spread }]
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
        _cfbRepo.GetSpreadsForSlateAsync(1).Returns([MakeSpread(DateTimeOffset.UtcNow.AddHours(2))]);
        var existing = new List<CfbPicks>
        {
            new() { UserId = UserId, LeagueId = 1, CfbSlateId = 1, Team = "ORE" }
        };
        _repo.GetUserPicksAsync(1, 1, UserId).Returns(existing);

        var request = new AddCfbPicksRequest
        {
            LeagueId = 1, CfbSlateId = 1, Season = 2025,
            Picks = [new CfbPickItem { Team = "ORE", PickType = PickType.Spread }]
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
            Picks = [new CfbPickItem { Team = "ORE", PickType = PickType.Spread }]
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
        _cfbRepo.GetSpreadsForSlateAsync(1).Returns([MakeSpread(DateTimeOffset.UtcNow.AddHours(2))]);
        _repo.GetUserPicksAsync(1, 1, UserId).Returns([]);
        var request = new AddCfbPicksRequest
        {
            LeagueId = 1, CfbSlateId = 1, Season = 2025,
            Picks = [new CfbPickItem { Team = "ORE", PickType = PickType.Spread }]
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
        _cfbRepo.GetSpreadsForSlateAsync(1).Returns([MakeSpread(DateTimeOffset.UtcNow.AddHours(-2))]);
        _repo.GetUserPicksAsync(1, 1, UserId).Returns([]);
        var request = new AddCfbPicksRequest
        {
            LeagueId = 1, CfbSlateId = 1, Season = 2025,
            Picks = [new CfbPickItem { Team = "ORE", PickType = PickType.Spread }]
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
        _cfbRepo.GetSpreadsForSlateAsync(1).Returns([MakeSpread(DateTimeOffset.UtcNow.AddHours(2))]);
        _repo.GetUserPicksAsync(1, 1, UserId).Returns([]);
        _repo.AddPicksAsync(Arg.Any<IEnumerable<CfbPicks>>()).Returns(Task.CompletedTask);
        var request = new AddCfbPicksRequest
        {
            LeagueId = 1, CfbSlateId = 1, Season = 2025,
            Picks = [new CfbPickItem { Team = "ORE", PickType = PickType.Spread }]
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
            Picks = [new CfbPickItem { Team = "ORE", PickType = PickType.Spread }]
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
            MakeSpread(DateTimeOffset.UtcNow.AddHours(2), "ORE", "OSU"),
            MakeSpread(DateTimeOffset.UtcNow.AddHours(2), "ALA", "UGA"),
        ]);
        _repo.GetUserPicksAsync(1, 1, UserId).Returns([]);
        var request = new AddCfbPicksRequest
        {
            LeagueId = 1, CfbSlateId = 1, Season = 2025,
            Picks =
            [
                new CfbPickItem { Team = "ORE", PickType = PickType.Spread },
                new CfbPickItem { Team = "ALA", PickType = PickType.Spread },
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
        _cfbRepo.GetSpreadsForSlateAsync(1).Returns([MakeSpread(DateTimeOffset.UtcNow.AddHours(2))]);
        _repo.GetAllPicksForSlateAsync(1, 1).Returns(
        [
            new CfbPickDto { UserId = UserId, Team = "ORE" },
            new CfbPickDto { UserId = OtherUserId, Team = "OSU" },
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
        _cfbRepo.GetSpreadsForSlateAsync(1).Returns([MakeSpread(DateTimeOffset.UtcNow.AddHours(-1))]);
        _repo.GetAllPicksForSlateAsync(1, 1).Returns(
        [
            new CfbPickDto { UserId = UserId, Team = "ORE" },
            new CfbPickDto { UserId = OtherUserId, Team = "OSU" },
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
        _cfbRepo.GetSpreadsForSlateAsync(1).Returns([MakeSpread(DateTimeOffset.UtcNow.AddHours(2))]);
        _repo.GetAllPicksForSlateAsync(1, 1).Returns(
        [
            new CfbPickDto { UserId = UserId, Team = "ORE" },
            new CfbPickDto { UserId = OtherUserId, Team = "OSU" },
        ]);

        var result = await BuildController("admin-001", isAdmin: true).GetAllPicks(1, 1);

        var ok = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsAssignableFrom<IEnumerable<CfbPickDto>>(ok.Value).ToList();
        Assert.Equal(2, returned.Count);
    }

    // ── DeletePicks — admin-only, targets correct userId ─────────────────────

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

    [Fact]
    public async Task DeletePicks_DeletesTargetUserId_NotAdminId()
    {
        const string targetUserId = "target-player-999";
        _repo.DeletePicksAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>()).Returns(Task.CompletedTask);

        var result = await BuildController("admin-001", isAdmin: true).DeletePicks(1, 1, targetUserId);

        Assert.IsType<OkResult>(result);
        await _repo.Received(1).DeletePicksAsync(1, 1, targetUserId);
        await _repo.DidNotReceive().DeletePicksAsync(1, 1, "admin-001");
    }

    // ── GetSpreads — serving-layer eligibility filter (frizat-9m0) ──────────
    // ── GetSpreads — league juice on the displayed spread (shared SpreadCalculator) ─────────

    [Fact]
    public async Task GetSpreads_ReturnsOnlyLeagueEligibleSpreads()
    {
        _cfbRepo.GetSpreadsForSlateAsync(1).Returns([
            MakeSpread(DateTimeOffset.UtcNow.AddHours(2), "ORE", "OSU", isLeagueEligible: true),
            MakeSpread(DateTimeOffset.UtcNow.AddHours(2), "TOL", "BALLST", isLeagueEligible: false),
        ]);
        _cfbRepo.GetSlateByIdAsync(1).Returns(MakeSlate());
        _leagueRepo.GetLeagueJuiceMappingAsync(1, 2025).Returns(new LeagueJuiceMapping { Juice = 0 });

        var result = await BuildController().GetSpreads(1, 1);

        var ok = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsAssignableFrom<IEnumerable<CfbSpreadDto>>(ok.Value).ToList();
        Assert.Single(returned);
        Assert.Equal("ORE", returned[0].HomeTeam);
    }

    [Fact]
    public async Task GetSpreads_AppliesLeagueJuiceToBothSidesOfTheSpread()
    {
        // Slate 1 -> regular-season tier -> Juice (not JuiceDivisional/JuiceConference).
        _cfbRepo.GetSpreadsForSlateAsync(1).Returns([
            MakeSpread(DateTimeOffset.UtcNow.AddHours(2), "ORE", "OSU"),
        ]);
        _cfbRepo.GetSlateByIdAsync(1).Returns(MakeSlate(slateNumber: 1));
        _leagueRepo.GetLeagueJuiceMappingAsync(1, 2025).Returns(new LeagueJuiceMapping { Juice = 3 });

        var result = await BuildController().GetSpreads(1, 1);

        var ok = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsAssignableFrom<IEnumerable<CfbSpreadDto>>(ok.Value).ToList();
        var spread = Assert.Single(returned);
        // MakeSpread leaves HomeTeamSpread/AwayTeamSpread at their default (0) raw values.
        Assert.Equal(3, spread.HomeTeamSpread);
        Assert.Equal(3, spread.AwayTeamSpread);
    }

    [Fact]
    public async Task GetSpreads_UsesSlateNumberTierForJuice_NotRegularSeasonJuice()
    {
        // Slate 16 -> quarterfinal tier -> JuiceDivisional.
        _cfbRepo.GetSpreadsForSlateAsync(1).Returns([
            MakeSpread(DateTimeOffset.UtcNow.AddHours(2), "ORE", "OSU"),
        ]);
        _cfbRepo.GetSlateByIdAsync(1).Returns(MakeSlate(slateNumber: 16));
        _leagueRepo.GetLeagueJuiceMappingAsync(1, 2025).Returns(
            new LeagueJuiceMapping { Juice = 3, JuiceDivisional = 10, JuiceConference = 6 });

        var result = await BuildController().GetSpreads(1, 1);

        var ok = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsAssignableFrom<IEnumerable<CfbSpreadDto>>(ok.Value).ToList();
        var spread = Assert.Single(returned);
        Assert.Equal(10, spread.HomeTeamSpread);
    }

    [Fact]
    public async Task GetSpreads_IncludesTeamRanksInResponse()
    {
        // Rank is read back from CfbRanking via GetLatestRankingsForWeekAsync
        // (CfbRankingCaptureJob/CfbSpreadJob already capture it there) — "most recent capture
        // wins" is resolved by that repository method's own join, not here; see
        // CfbRepositoryTests for that behavior.
        var spread = MakeSpread(DateTimeOffset.UtcNow.AddHours(2), "ORE", "OSU");
        _cfbRepo.GetSpreadsForSlateAsync(1).Returns([spread]);
        _cfbRepo.GetSlateByIdAsync(1).Returns(MakeSlate(espnWeekNumber: 3));
        _leagueRepo.GetLeagueJuiceMappingAsync(1, 2025).Returns(new LeagueJuiceMapping { Juice = 0 });
        _cfbRepo.GetLatestRankingsForWeekAsync(2025, 3).Returns(new Dictionary<string, int> {
            ["ORE"] = 5,
            ["OSU"] = 99, // unranked
        });

        var result = await BuildController().GetSpreads(1, 1);

        var ok = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsAssignableFrom<IEnumerable<CfbSpreadDto>>(ok.Value).ToList();
        var dto = Assert.Single(returned);
        Assert.Equal(5, dto.HomeTeamRank);
        Assert.Null(dto.AwayTeamRank);
    }

    [Fact]
    public async Task GetSpreads_HasNullRanks_WhenSlateHasNoEspnWeekNumber()
    {
        var spread = MakeSpread(DateTimeOffset.UtcNow.AddHours(2), "ORE", "OSU");
        _cfbRepo.GetSpreadsForSlateAsync(1).Returns([spread]);
        _cfbRepo.GetSlateByIdAsync(1).Returns(MakeSlate(espnWeekNumber: null));
        _leagueRepo.GetLeagueJuiceMappingAsync(1, 2025).Returns(new LeagueJuiceMapping { Juice = 0 });

        var result = await BuildController().GetSpreads(1, 1);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.Single(Assert.IsAssignableFrom<IEnumerable<CfbSpreadDto>>(ok.Value));
        Assert.Null(dto.HomeTeamRank);
        Assert.Null(dto.AwayTeamRank);
        await _cfbRepo.DidNotReceive().GetLatestRankingsForWeekAsync(Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public async Task GetSpreads_ReturnsForbid_WhenUserNotInLeague()
    {
        _leagueRepo.UserExistsInLeagueAsync(UserId, 1).Returns(false);

        var result = await BuildController().GetSpreads(1, 1);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task GetSpreads_AdminCanViewRegardlessOfMembership()
    {
        _leagueRepo.UserExistsInLeagueAsync(Arg.Any<string>(), 1).Returns(false);
        _cfbRepo.GetSpreadsForSlateAsync(1).Returns([
            MakeSpread(DateTimeOffset.UtcNow.AddHours(2), "ORE", "OSU"),
        ]);
        _cfbRepo.GetSlateByIdAsync(1).Returns(MakeSlate());
        _leagueRepo.GetLeagueJuiceMappingAsync(1, 2025).Returns(new LeagueJuiceMapping { Juice = 0 });

        var result = await BuildController("admin-001", isAdmin: true).GetSpreads(1, 1);

        Assert.IsType<OkObjectResult>(result);
    }

    // ── GetScores — serving-layer eligibility filter (frizat-9m0) ───────────

    private static CfbScores MakeScore(string home = "ORE", string away = "OSU") => new()
    {
        CfbSlateId = 1, HomeTeam = home, AwayTeam = away,
        HomeTeamScore = 24, AwayTeamScore = 17, GameStatus = TypeName.StatusFinal,
    };

    [Fact]
    public async Task GetScores_ExcludesScoresForKnownIneligibleEvents()
    {
        _cfbRepo.GetSpreadsForSlateAsync(1).Returns([
            MakeSpread(DateTimeOffset.UtcNow.AddHours(-2), "ORE", "OSU", isLeagueEligible: true),
            MakeSpread(DateTimeOffset.UtcNow.AddHours(-2), "TOL", "BALLST", isLeagueEligible: false),
        ]);
        _cfbRepo.GetScoresForSlateAsync(1).Returns([
            MakeScore("ORE", "OSU"),
            MakeScore("TOL", "BALLST"),
        ]);

        var result = await BuildController().GetScores(1);

        var ok = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsAssignableFrom<IEnumerable<CfbScoreDto>>(ok.Value).ToList();
        Assert.Single(returned);
        Assert.Equal("ORE", returned[0].HomeTeam);
    }

    [Fact]
    public async Task GetScores_IsFailOpen_ForScoreWithNoMatchingSpreadAtAll()
    {
        // Same fail-open philosophy as AddPicks_WhenNoMatchingSpread_AllowsPick: a completed game
        // we have no spread data for at all (e.g. CfbSpreadJob's once-a-week fetch missed a late
        // schedule change) should still show, not silently vanish — only games we POSITIVELY know
        // are ineligible get excluded.
        _cfbRepo.GetSpreadsForSlateAsync(1).Returns([]);
        _cfbRepo.GetScoresForSlateAsync(1).Returns([MakeScore("ORE", "OSU")]);

        var result = await BuildController().GetScores(1);

        var ok = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsAssignableFrom<IEnumerable<CfbScoreDto>>(ok.Value).ToList();
        Assert.Single(returned);
        Assert.Equal("ORE", returned[0].HomeTeam);
    }

    // ── AddPicks — ineligible-team guard (frizat-9m0) ────────────────────────
    // Rejects a pick for a team we KNOW is excluded (MAC Tue/Wed, unranked, etc.) — distinct
    // from AddPicks_WhenNoMatchingSpread_AllowsPick above, which fails open when we have NO data
    // for the game at all (e.g. ESPN cache gap). Positive knowledge of ineligibility rejects;
    // absence of knowledge does not.

    [Fact]
    public async Task AddPicks_WhenTeamIsKnownIneligible_ReturnsBadRequest()
    {
        _cfbRepo.GetSlateByIdAsync(1).Returns(MakeSlate());
        _cfbRepo.GetSpreadsForSlateAsync(1).Returns([
            MakeSpread(DateTimeOffset.UtcNow.AddHours(2), isLeagueEligible: false),
        ]);
        _repo.GetUserPicksAsync(1, 1, UserId).Returns([]);
        var request = new AddCfbPicksRequest
        {
            LeagueId = 1, CfbSlateId = 1, Season = 2025,
            Picks = [new CfbPickItem { Team = "ORE", PickType = PickType.Spread }]
        };

        var result = await BuildController().AddPicks(request);

        Assert.IsType<BadRequestObjectResult>(result);
        await _repo.DidNotReceive().AddPicksAsync(Arg.Any<IEnumerable<CfbPicks>>());
    }
}
