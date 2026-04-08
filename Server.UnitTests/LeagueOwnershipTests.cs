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
            Substitute.For<IEspnCacheService>());

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
            Substitute.For<IEspnCacheService>());

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
            Substitute.For<IEspnCacheService>());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = BuildPrincipal(AttackerId, isAdmin: true) }
        };

        var result = await controller.GetLeagueUserMappingsForUser(OwnerId);

        Assert.IsType<OkObjectResult>(result.Result);
    }
}
