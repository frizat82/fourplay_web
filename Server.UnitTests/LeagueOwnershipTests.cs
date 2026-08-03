using FourPlayWebApp.Server.Controllers;
using FourPlayWebApp.Server.Models;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models.Data.Dtos;
using FourPlayWebApp.Shared.Models.Enum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using System.Reflection;
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

    // frizat-d6l: self-serve league creation — any authenticated user may create a league now,
    // but a non-admin's request must always make THEM the owner server-side, regardless of what
    // ownerUserId they send, to close a privilege-escalation hole (assigning ownership to/of
    // someone else's league without that person's involvement).
    [Fact]
    public async Task CreateLeague_NonAdmin_ForcesOwnerToCaller_IgnoringSpoofedOwnerUserId()
    {
        var (ctrl, repo) = BuildControllerWithRepo(BuildPrincipal(OwnerId));
        repo.LeagueExistsAsync(Arg.Any<string>()).Returns(false);
        var createdLeague = new LeagueInfo { Id = 42, LeagueName = "My League", OwnerUserId = OwnerId, LeagueType = LeagueType.Nfl };
        repo.AddLeagueInfoAsync(Arg.Any<LeagueInfo>()).Returns(Task.FromResult(createdLeague));

        // AttackerId is spoofed as the owner in the request body — caller is OwnerId, not admin.
        var dto = new LeagueCreateDto("My League", LeagueType.Nfl, AttackerId, 2025, 0, 0, 0, 0);
        var result = await ctrl.CreateLeague(dto) as OkObjectResult;

        Assert.NotNull(result);
        await repo.Received(1).AddLeagueInfoAsync(Arg.Is<LeagueInfo>(l => l.OwnerUserId == OwnerId));
        await repo.Received(1).AddLeagueUserMappingAsync(Arg.Is<LeagueUserMapping>(m => m.UserId == OwnerId));
    }

    [Fact]
    public void CreateLeague_IsNotRestrictedToAdministratorRole()
    {
        var method = typeof(LeagueController).GetMethod(nameof(LeagueController.CreateLeague));
        Assert.NotNull(method);

        var roleAttr = method!.GetCustomAttributes<AuthorizeAttribute>().FirstOrDefault(a => a.Roles is not null);

        Assert.Null(roleAttr); // any authenticated user may call this now — class-level [Authorize] still applies
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

    [Fact]
    public async Task GetAllLeagues_ReturnsLeaguesAcrossAllOwnersAndSports()
    {
        var (ctrl, repo) = BuildControllerWithRepo(BuildPrincipal(AttackerId, isAdmin: true));
        repo.GetAllLeaguesAsync().Returns(
        [
            new LeagueInfo { Id = 1, LeagueName = "NFL League", OwnerUserId = OwnerId, LeagueType = LeagueType.Nfl },
            new LeagueInfo { Id = 2, LeagueName = "CFB League", OwnerUserId = "some-other-owner", LeagueType = LeagueType.Cfb },
        ]);

        var result = await ctrl.GetAllLeagues() as OkObjectResult;

        var leagues = Assert.IsAssignableFrom<IEnumerable<LeagueInfoDto>>(result!.Value);
        Assert.Equal(2, leagues.Count());
    }

    [Fact]
    public async Task GetLeagueUserMappings_ReturnsForbid_WhenCallerIsNotOwnerOrAdmin()
    {
        var (ctrl, repo) = BuildControllerWithRepo(BuildPrincipal(AttackerId));
        repo.GetLeagueInfoAsync(1).Returns(new LeagueInfo { Id = 1, OwnerUserId = OwnerId, LeagueName = "L" });

        var result = await ctrl.GetLeagueUserMappings(1);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task GetLeagueUserMappings_ReturnsOk_WhenCallerIsOwner()
    {
        var (ctrl, repo) = BuildControllerWithRepo(BuildPrincipal(OwnerId));
        repo.GetLeagueInfoAsync(1).Returns(new LeagueInfo { Id = 1, OwnerUserId = OwnerId, LeagueName = "L" });
        repo.GetLeagueUserMappingsAsync(1).Returns(
        [
            new LeagueUserMapping
            {
                LeagueId = 1,
                UserId = OwnerId,
                League = new LeagueInfo { Id = 1, LeagueName = "L", OwnerUserId = OwnerId, LeagueType = LeagueType.Nfl },
                User = new ApplicationUser { Id = OwnerId, UserName = "owner" },
            },
        ]);

        var result = await ctrl.GetLeagueUserMappings(1);

        Assert.IsType<OkObjectResult>(result.Result);
        await repo.Received(1).GetLeagueUserMappingsAsync(1);
    }

    [Fact]
    public async Task GetLeagueUserMappings_IncludesMemberEmail_NotJustUserId()
    {
        var (ctrl, repo) = BuildControllerWithRepo(BuildPrincipal(OwnerId));
        repo.GetLeagueInfoAsync(1).Returns(new LeagueInfo { Id = 1, OwnerUserId = OwnerId, LeagueName = "L" });
        repo.GetLeagueUserMappingsAsync(1).Returns(
        [
            new LeagueUserMapping
            {
                LeagueId = 1,
                UserId = OwnerId,
                League = new LeagueInfo { Id = 1, LeagueName = "L", OwnerUserId = OwnerId, LeagueType = LeagueType.Nfl },
                User = new ApplicationUser { Id = OwnerId, UserName = "owner", Email = "owner@example.com" },
            },
        ]);

        var result = await ctrl.GetLeagueUserMappings(1);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsAssignableFrom<List<LeagueUserMappingDto>>(ok.Value);
        Assert.Equal("owner@example.com", dtos[0].Email);
    }

    [Fact]
    public async Task InviteToLeague_ReturnsForbid_WhenCallerIsNotOwnerOrAdmin()
    {
        var (ctrl, repo) = BuildControllerWithRepo(BuildPrincipal(AttackerId));
        repo.GetLeagueInfoAsync(1).Returns(new LeagueInfo { Id = 1, OwnerUserId = OwnerId, LeagueName = "L" });

        var result = await ctrl.InviteToLeague(1, new LeagueInviteDto("target@example.com"));

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task InviteToLeague_ReturnsOk_WhenCallerIsOwner()
    {
        var invSvc = Substitute.For<IInvitationService>();
        invSvc.CreateInvitationAsync("target@example.com", OwnerId, 1)
              .Returns(new Invitation { Id = 1, Email = "target@example.com", InvitationCode = "code-abc" });

        var repo = Substitute.For<ILeagueRepository>();
        repo.GetLeagueInfoAsync(1).Returns(new LeagueInfo { Id = 1, OwnerUserId = OwnerId, LeagueName = "L" });

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
            invSvc);
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = BuildPrincipal(OwnerId) }
        };

        var result = await ctrl.InviteToLeague(1, new LeagueInviteDto("target@example.com"));

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task InviteToLeague_PassesBaseUrlThrough_SoTheEmailCanBeSentServerSide()
    {
        var invSvc = Substitute.For<IInvitationService>();
        invSvc.CreateInvitationAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string?>())
              .Returns(new Invitation { Id = 1, Email = "target@example.com", InvitationCode = "code-abc" });

        var repo = Substitute.For<ILeagueRepository>();
        repo.GetLeagueInfoAsync(1).Returns(new LeagueInfo { Id = 1, OwnerUserId = OwnerId, LeagueName = "L" });

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
            invSvc);
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = BuildPrincipal(OwnerId) }
        };

        await ctrl.InviteToLeague(1, new LeagueInviteDto("target@example.com", "https://ivleague.com"));

        await invSvc.Received(1).CreateInvitationAsync("target@example.com", OwnerId, 1, baseUrl: "https://ivleague.com");
    }

    [Fact]
    public async Task AssignLeagueOwner_CallsRepo_WhenCallerIsAdmin()
    {
        var (ctrl, repo) = BuildControllerWithRepo(BuildPrincipal(AttackerId, isAdmin: true));

        var result = await ctrl.AssignLeagueOwner(1, "new-owner");

        Assert.IsType<NoContentResult>(result);
        await repo.Received(1).UpdateLeagueOwnerAsync(1, "new-owner");
    }

    [Fact]
    public async Task CreateLeague_ReturnsConflict_WhenLeagueNameAlreadyExists()
    {
        var (ctrl, repo) = BuildControllerWithRepo(BuildPrincipal(AttackerId, isAdmin: true));
        repo.LeagueExistsAsync(Arg.Any<string>()).Returns(true);

        var dto = new LeagueCreateDto("Existing League", LeagueType.Nfl, OwnerId, 2025, 13, 10, 6, 5);
        var result = await ctrl.CreateLeague(dto);

        Assert.IsType<ConflictObjectResult>(result);
    }

    // ─── mon.6: membership guards on info / juice / userExists ─────────────

    private static LeagueInfo StubLeague(int id = 1) =>
        new() { Id = id, LeagueName = "Test", OwnerUserId = OwnerId };

    // GetLeagueInfo

    [Fact]
    public async Task GetLeagueInfo_ReturnsForbid_ForNonMember()
    {
        var (ctrl, repo) = BuildControllerWithRepo(BuildPrincipal(AttackerId));
        repo.GetLeagueInfoAsync(1).Returns(StubLeague());
        repo.UserExistsInLeagueAsync(AttackerId, 1).Returns(false);

        var result = await ctrl.GetLeagueInfo(1);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task GetLeagueInfo_ReturnsOk_ForMember()
    {
        const string memberId = "member-003";
        var (ctrl, repo) = BuildControllerWithRepo(BuildPrincipal(memberId));
        repo.GetLeagueInfoAsync(1).Returns(StubLeague());
        repo.UserExistsInLeagueAsync(memberId, 1).Returns(true);

        var result = await ctrl.GetLeagueInfo(1);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetLeagueInfo_ReturnsOk_ForAdmin()
    {
        var (ctrl, repo) = BuildControllerWithRepo(BuildPrincipal(AttackerId, isAdmin: true));
        repo.GetLeagueInfoAsync(1).Returns(StubLeague());

        var result = await ctrl.GetLeagueInfo(1);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    // GetLeagueJuice

    [Fact]
    public async Task GetLeagueJuice_ReturnsForbid_ForNonMember()
    {
        var (ctrl, repo) = BuildControllerWithRepo(BuildPrincipal(AttackerId));
        repo.UserExistsInLeagueAsync(AttackerId, 1).Returns(false);

        var result = await ctrl.GetLeagueJuice(1);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task GetLeagueJuice_ReturnsOk_ForMember()
    {
        const string memberId = "member-003";
        var (ctrl, repo) = BuildControllerWithRepo(BuildPrincipal(memberId));
        repo.UserExistsInLeagueAsync(memberId, 1).Returns(true);
        repo.GetLeagueJuiceMappingAsync(1).Returns([]);

        var result = await ctrl.GetLeagueJuice(1);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetLeagueJuice_ReturnsOk_ForAdmin()
    {
        var (ctrl, repo) = BuildControllerWithRepo(BuildPrincipal(AttackerId, isAdmin: true));
        repo.GetLeagueJuiceMappingAsync(1).Returns([]);

        var result = await ctrl.GetLeagueJuice(1);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    // GetLeagueJuiceForSeason

    [Fact]
    public async Task GetLeagueJuiceForSeason_ReturnsForbid_ForNonMember()
    {
        var (ctrl, repo) = BuildControllerWithRepo(BuildPrincipal(AttackerId));
        repo.UserExistsInLeagueAsync(AttackerId, 1).Returns(false);

        var result = await ctrl.GetLeagueJuiceForSeason(1, 2025);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task GetLeagueJuiceForSeason_ReturnsOk_ForMember()
    {
        const string memberId = "member-003";
        var (ctrl, repo) = BuildControllerWithRepo(BuildPrincipal(memberId));
        repo.UserExistsInLeagueAsync(memberId, 1).Returns(true);
        repo.GetLeagueJuiceMappingAsync(1, 2025).Returns((LeagueJuiceMapping?)null);

        var result = await ctrl.GetLeagueJuiceForSeason(1, 2025);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetLeagueJuiceForSeason_ReturnsOk_ForAdmin()
    {
        var (ctrl, repo) = BuildControllerWithRepo(BuildPrincipal(AttackerId, isAdmin: true));
        repo.GetLeagueJuiceMappingAsync(1, 2025).Returns((LeagueJuiceMapping?)null);

        var result = await ctrl.GetLeagueJuiceForSeason(1, 2025);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    // UserExistsInLeague — self-lookup or admin only

    [Fact]
    public async Task UserExistsInLeague_ReturnsForbid_ForNonSelf()
    {
        var (ctrl, _) = BuildControllerWithRepo(BuildPrincipal(AttackerId));

        var result = await ctrl.UserExistsInLeague(OwnerId, 1);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task UserExistsInLeague_ReturnsOk_ForSelf()
    {
        var (ctrl, repo) = BuildControllerWithRepo(BuildPrincipal(OwnerId));
        repo.UserExistsInLeagueAsync(OwnerId, 1).Returns(true);

        var result = await ctrl.UserExistsInLeague(OwnerId, 1);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task UserExistsInLeague_ReturnsOk_ForAdmin()
    {
        var (ctrl, repo) = BuildControllerWithRepo(BuildPrincipal(AttackerId, isAdmin: true));
        repo.UserExistsInLeagueAsync(OwnerId, 1).Returns(false);

        var result = await ctrl.UserExistsInLeague(OwnerId, 1);

        Assert.IsType<OkObjectResult>(result.Result);
    }
}
