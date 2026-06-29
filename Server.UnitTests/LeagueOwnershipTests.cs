using FourPlayWebApp.Server.Controllers;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models.Data.Dtos;
using FourPlayWebApp.Shared.Models.Enum;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using System.Security.Claims;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// frizat-uvi: Ownership checks on LeagueController endpoints that previously
/// allowed any authenticated user to read another user's data.
/// </summary>
public class LeagueOwnershipTests
{
    private const string OwnerId  = "owner-001";
    private const string AttackerId = "attacker-002";

    private static LeagueController BuildController(ClaimsPrincipal principal)
    {
        var store       = Substitute.For<IUserStore<ApplicationUser>>();
        var userManager = Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);

        var controller = new LeagueController(
            new MemoryCache(new MemoryCacheOptions()),
            Substitute.For<ILeagueRepository>(),
            NullLogger<LeagueController>.Instance,
            userManager,
            Substitute.For<ISpreadCalculatorBuilder>(),
            Substitute.For<IEspnCacheService>(),
            Substitute.For<IInvitationService>());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
        return controller;
    }

    private static ClaimsPrincipal BuildPrincipal(string userId, bool isAdmin = false)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, userId),
        };
        if (isAdmin)
            claims.Add(new Claim(ClaimTypes.Role, "Administrator"));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    /// <summary>
    /// frizat-uvi: GetLeagueUserMappingsForUser must return 403 when a non-admin
    /// caller requests mappings for a different user (IDOR prevention).
    /// </summary>
    [Fact]
    public async Task GetLeagueUserMappingsForUser_ReturnsForbid_WhenCallerRequestsAnotherUser()
    {
        var controller = BuildController(BuildPrincipal(AttackerId));

        var result = await controller.GetLeagueUserMappingsForUser(OwnerId);

        Assert.IsType<ForbidResult>(result.Result);
    }

    /// <summary>
    /// frizat-uvi: GetLeagueUserMappingsForUser must allow a user to request their own mappings.
    /// </summary>
    [Fact]
    public async Task GetLeagueUserMappingsForUser_ReturnsOk_WhenCallerRequestsOwnMappings()
    {
        var repo = Substitute.For<ILeagueRepository>();
        repo.GetLeagueUserMappingsAsync(Arg.Any<ApplicationUser>()).Returns([]);

        var store       = Substitute.For<IUserStore<ApplicationUser>>();
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
            HttpContext = new DefaultHttpContext { User = BuildPrincipal(OwnerId) }
        };

        var result = await controller.GetLeagueUserMappingsForUser(OwnerId);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    /// <summary>
    /// frizat-uvi: Admins must be able to request any user's league mappings.
    /// </summary>
    [Fact]
    public async Task GetLeagueUserMappingsForUser_ReturnsOk_WhenCallerIsAdmin()
    {
        var repo = Substitute.For<ILeagueRepository>();
        repo.GetLeagueUserMappingsAsync(Arg.Any<ApplicationUser>()).Returns([]);

        var store       = Substitute.For<IUserStore<ApplicationUser>>();
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
            HttpContext = new DefaultHttpContext { User = BuildPrincipal(AttackerId, isAdmin: true) }
        };

        var result = await controller.GetLeagueUserMappingsForUser(OwnerId);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    // ─── Commissioner Portal: owner-scoped endpoint tests ───────────────────

    private static (LeagueController ctrl, ILeagueRepository repo) BuildControllerWithRepo(ClaimsPrincipal principal)
    {
        var repo = Substitute.For<ILeagueRepository>();
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        var userManager = Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);
        var ctrl = new LeagueController(
            new MemoryCache(new MemoryCacheOptions()),
            repo,
            NullLogger<LeagueController>.Instance,
            userManager,
            Substitute.For<ISpreadCalculatorBuilder>(),
            Substitute.For<IEspnCacheService>(),
            Substitute.For<IInvitationService>());
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
        return (ctrl, repo);
    }

    [Fact]
    public async Task UpdateJuice_ReturnsForbid_WhenCallerIsNotOwnerOrAdmin()
    {
        var (ctrl, repo) = BuildControllerWithRepo(BuildPrincipal(AttackerId));
        repo.GetLeagueInfoAsync(1).Returns(new LeagueInfo { Id = 1, OwnerUserId = OwnerId, LeagueName = "L" });

        var result = await ctrl.UpdateLeagueJuice(1, 2025, new LeagueJuiceUpdateDto(13, 10, 6, 5));

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task UpdateJuice_ReturnsNoContent_WhenCallerIsOwner()
    {
        var (ctrl, repo) = BuildControllerWithRepo(BuildPrincipal(OwnerId));
        repo.GetLeagueInfoAsync(1).Returns(new LeagueInfo { Id = 1, OwnerUserId = OwnerId, LeagueName = "L" });
        repo.GetLeagueJuiceMappingAsync(1, 2025).Returns(new LeagueJuiceMapping { Id = 5, LeagueId = 1, Season = 2025, Juice = 13, JuiceDivisional = 10, JuiceConference = 6, WeeklyCost = 5 });

        var result = await ctrl.UpdateLeagueJuice(1, 2025, new LeagueJuiceUpdateDto(14, 11, 7, 10));

        Assert.IsType<NoContentResult>(result);
        await repo.Received(1).UpdateLeagueJuiceMappingAsync(Arg.Is<LeagueJuiceMapping>(m => m.Juice == 14 && m.JuiceDivisional == 11));
    }

    [Fact]
    public async Task UpdateJuice_ReturnsNoContent_WhenCallerIsAdmin()
    {
        var (ctrl, repo) = BuildControllerWithRepo(BuildPrincipal(AttackerId, isAdmin: true));
        repo.GetLeagueInfoAsync(1).Returns(new LeagueInfo { Id = 1, OwnerUserId = OwnerId, LeagueName = "L" });
        repo.GetLeagueJuiceMappingAsync(1, 2025).Returns(new LeagueJuiceMapping { Id = 5, LeagueId = 1, Season = 2025 });

        var result = await ctrl.UpdateLeagueJuice(1, 2025, new LeagueJuiceUpdateDto(13, 10, 6, 5));

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task RemoveMember_ReturnsForbid_WhenCallerIsNotOwnerOrAdmin()
    {
        var (ctrl, repo) = BuildControllerWithRepo(BuildPrincipal(AttackerId));
        repo.GetLeagueInfoAsync(1).Returns(new LeagueInfo { Id = 1, OwnerUserId = OwnerId, LeagueName = "L" });

        var result = await ctrl.RemoveLeagueMember(1, "victim-003");

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task RemoveMember_ReturnsNoContent_WhenCallerIsOwner()
    {
        var (ctrl, repo) = BuildControllerWithRepo(BuildPrincipal(OwnerId));
        repo.GetLeagueInfoAsync(1).Returns(new LeagueInfo { Id = 1, OwnerUserId = OwnerId, LeagueName = "L" });

        var result = await ctrl.RemoveLeagueMember(1, "victim-003");

        Assert.IsType<NoContentResult>(result);
        await repo.Received(1).RemoveLeagueUserMappingAsync(1, "victim-003");
    }

    [Fact]
    public async Task GetLeagueCost_ReturnsCorrectCostForBaseTier()
    {
        var (ctrl, repo) = BuildControllerWithRepo(BuildPrincipal(OwnerId));
        repo.GetLeagueInfoAsync(1).Returns(new LeagueInfo { Id = 1, OwnerUserId = OwnerId, LeagueName = "L" });
        repo.GetLeagueMemberCountAsync(1).Returns(8);

        var result = await ctrl.GetLeagueCost(1) as OkObjectResult;

        Assert.NotNull(result);
        var dto = Assert.IsType<LeagueCostDto>(result.Value);
        Assert.Equal(8, dto.MemberCount);
        Assert.Equal(100m, dto.Cost);
    }

    [Fact]
    public async Task GetLeagueCost_ReturnsCorrectCostForOverage()
    {
        var (ctrl, repo) = BuildControllerWithRepo(BuildPrincipal(OwnerId));
        repo.GetLeagueInfoAsync(1).Returns(new LeagueInfo { Id = 1, OwnerUserId = OwnerId, LeagueName = "L" });
        repo.GetLeagueMemberCountAsync(1).Returns(12);

        var result = await ctrl.GetLeagueCost(1) as OkObjectResult;

        var dto = Assert.IsType<LeagueCostDto>(result!.Value);
        Assert.Equal(120m, dto.Cost);  // $100 + 2 * $10
    }

    [Fact]
    public async Task RollForwardJuice_CopiesAllFieldsFromPriorSeason()
    {
        var (ctrl, repo) = BuildControllerWithRepo(BuildPrincipal(OwnerId));
        var prior = new LeagueJuiceMapping { LeagueId = 1, Season = 2025, Juice = 13, JuiceDivisional = 10, JuiceConference = 6, WeeklyCost = 5 };
        repo.GetLeagueInfoAsync(1).Returns(new LeagueInfo { Id = 1, OwnerUserId = OwnerId, LeagueName = "L" });
        repo.GetLeagueJuiceMappingAsync(1, 2026).Returns((LeagueJuiceMapping?)null);
        repo.GetLeagueJuiceMappingAsync(1).Returns(new List<LeagueJuiceMapping> { prior });

        var result = await ctrl.RollForwardJuice(1, 2026);

        Assert.IsType<NoContentResult>(result);
        await repo.Received(1).AddLeagueJuiceMappingAsync(Arg.Is<LeagueJuiceMapping>(m =>
            m.Season == 2026 && m.Juice == 13 && m.JuiceDivisional == 10 &&
            m.JuiceConference == 6 && m.WeeklyCost == 5));
    }

    [Fact]
    public async Task RollForwardJuice_ReturnsBadRequest_WhenTargetSeasonAlreadyExists()
    {
        var (ctrl, repo) = BuildControllerWithRepo(BuildPrincipal(OwnerId));
        repo.GetLeagueInfoAsync(1).Returns(new LeagueInfo { Id = 1, OwnerUserId = OwnerId, LeagueName = "L" });
        repo.GetLeagueJuiceMappingAsync(1, 2026).Returns(new LeagueJuiceMapping { LeagueId = 1, Season = 2026 });

        var result = await ctrl.RollForwardJuice(1, 2026);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateLeague_CreatesLeagueInfoAndJuiceMappingAtomically()
    {
        var (ctrl, repo) = BuildControllerWithRepo(BuildPrincipal(AttackerId, isAdmin: true));
        repo.LeagueExistsAsync(Arg.Any<string>()).Returns(false);
        var createdLeague = new LeagueInfo { Id = 42, LeagueName = "My League", OwnerUserId = OwnerId, LeagueType = LeagueType.Nfl };
        repo.AddLeagueInfoAsync(Arg.Any<LeagueInfo>()).Returns(Task.FromResult(createdLeague));

        var dto = new LeagueCreateDto("My League", LeagueType.Nfl, OwnerId, 2025, 13, 10, 6, 5);
        var result = await ctrl.CreateLeague(dto) as OkObjectResult;

        Assert.NotNull(result);
        await repo.Received(1).AddLeagueInfoAsync(Arg.Is<LeagueInfo>(l => l.LeagueName == "My League" && l.OwnerUserId == OwnerId));
        await repo.Received(1).AddLeagueJuiceMappingAsync(Arg.Is<LeagueJuiceMapping>(m => m.Season == 2025 && m.Juice == 13));
        await repo.Received(1).AddLeagueUserMappingAsync(Arg.Is<LeagueUserMapping>(m => m.UserId == OwnerId));
    }

    [Fact]
    public async Task GetMyLeagues_ReturnsOnlyLeaguesOwnedByCaller()
    {
        var (ctrl, repo) = BuildControllerWithRepo(BuildPrincipal(OwnerId));
        repo.GetLeaguesByOwnerAsync(OwnerId).Returns(
        [
            new LeagueInfo { Id = 1, LeagueName = "NFL League", OwnerUserId = OwnerId, LeagueType = LeagueType.Nfl },
            new LeagueInfo { Id = 2, LeagueName = "CFB League", OwnerUserId = OwnerId, LeagueType = LeagueType.Cfb },
        ]);

        var result = await ctrl.GetMyLeagues() as OkObjectResult;

        var leagues = Assert.IsAssignableFrom<IEnumerable<LeagueInfoDto>>(result!.Value);
        Assert.Equal(2, leagues.Count());
    }
}
