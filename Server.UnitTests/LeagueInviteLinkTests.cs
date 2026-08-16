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
using System.Security.Claims;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// PR #223: Shareable league invite link — ownership guards, generate, validate, join.
/// </summary>
public class LeagueInviteLinkTests
{
    private const string OwnerId = "owner-001";
    private const string MemberId = "member-002";
    private const string StrangerId = "stranger-003";

    private static LeagueController BuildController(
        ClaimsPrincipal principal,
        ILeagueRepository? repo = null,
        ILeagueInviteLinkService? linkService = null)
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        var userManager = Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);

        var controller = new LeagueController(
            new MemoryCache(new MemoryCacheOptions()),
            repo ?? Substitute.For<ILeagueRepository>(),
            NullLogger<LeagueController>.Instance,
            userManager,
            Substitute.For<ISpreadCalculatorBuilder>(),
            Substitute.For<IEspnCacheService>(),
            Substitute.For<IInvitationService>(),
            linkService ?? Substitute.For<ILeagueInviteLinkService>());

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

    // ── GenerateInviteLink ───────────────────────────────────────────────────

    [Fact]
    public async Task GenerateInviteLink_ReturnsForbid_WhenCallerIsNotOwnerOrAdmin()
    {
        var repo = Substitute.For<ILeagueRepository>();
        repo.GetLeagueInfoAsync(1).Returns(new LeagueInfo { Id = 1, OwnerUserId = OwnerId, LeagueName = "TestLeague" });

        var controller = BuildController(BuildPrincipal(StrangerId), repo: repo);

        var result = await controller.GenerateInviteLink(1);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task GenerateInviteLink_ReturnsOk_WhenCallerIsOwner()
    {
        var repo = Substitute.For<ILeagueRepository>();
        repo.GetLeagueInfoAsync(1).Returns(new LeagueInfo { Id = 1, OwnerUserId = OwnerId, LeagueName = "TestLeague" });

        var linkService = Substitute.For<ILeagueInviteLinkService>();
        linkService.GenerateAsync(1, OwnerId, Arg.Any<LeagueInfo>()).Returns(new LeagueInviteLink
        {
            Token = "abc123",
            LeagueId = 1,
            League = new LeagueInfo { Id = 1, LeagueName = "TestLeague" },
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(24),
        });

        var controller = BuildController(BuildPrincipal(OwnerId), repo: repo, linkService: linkService);

        var result = await controller.GenerateInviteLink(1);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<LeagueInviteLinkDto>(ok.Value);
        Assert.Equal("abc123", dto.Token);
        Assert.Equal("TestLeague", dto.LeagueName);
    }

    [Fact]
    public async Task GenerateInviteLink_ReturnsOk_WhenCallerIsAdmin()
    {
        var repo = Substitute.For<ILeagueRepository>();
        repo.GetLeagueInfoAsync(1).Returns(new LeagueInfo { Id = 1, OwnerUserId = OwnerId, LeagueName = "TestLeague" });

        var linkService = Substitute.For<ILeagueInviteLinkService>();
        linkService.GenerateAsync(1, Arg.Any<string>(), Arg.Any<LeagueInfo>()).Returns(new LeagueInviteLink
        {
            Token = "xyz789",
            LeagueId = 1,
            League = new LeagueInfo { Id = 1, LeagueName = "TestLeague" },
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(24),
        });

        var controller = BuildController(BuildPrincipal("admin-user", isAdmin: true), repo: repo, linkService: linkService);

        var result = await controller.GenerateInviteLink(1);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    // ── ValidateInviteLink ──────────────────────────────────────────────────

    [Fact]
    public async Task ValidateInviteLink_ReturnsNotFound_WhenTokenInvalid()
    {
        var linkService = Substitute.For<ILeagueInviteLinkService>();
        linkService.ValidateAsync("badtoken").Returns((LeagueInviteLink?)null);

        var controller = BuildController(BuildPrincipal(OwnerId), linkService: linkService);

        var result = await controller.ValidateInviteLink("badtoken");

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task ValidateInviteLink_ReturnsOk_WhenTokenValid()
    {
        var link = new LeagueInviteLink
        {
            Token = "valid123",
            LeagueId = 5,
            League = new LeagueInfo { Id = 5, LeagueName = "MyLeague" },
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(20),
        };
        var linkService = Substitute.For<ILeagueInviteLinkService>();
        linkService.ValidateAsync("valid123").Returns(link);

        var controller = BuildController(BuildPrincipal(MemberId), linkService: linkService);

        var result = await controller.ValidateInviteLink("valid123");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<LeagueInviteLinkDto>(ok.Value);
        Assert.Equal(5, dto.LeagueId);
        Assert.Equal("MyLeague", dto.LeagueName);
    }

    // ── JoinViaLink ─────────────────────────────────────────────────────────

    [Fact]
    public async Task JoinViaLink_ReturnsNotFound_WhenTokenInvalid()
    {
        var linkService = Substitute.For<ILeagueInviteLinkService>();
        linkService.ValidateAsync("bad").Returns((LeagueInviteLink?)null);

        var controller = BuildController(BuildPrincipal(MemberId), linkService: linkService);

        var result = await controller.JoinViaLink("bad");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task JoinViaLink_ReturnsConflict_WhenAlreadyMember()
    {
        var link = new LeagueInviteLink { Token = "tok", LeagueId = 1, League = new LeagueInfo { Id = 1, LeagueName = "L" }, ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) };
        var linkService = Substitute.For<ILeagueInviteLinkService>();
        linkService.ValidateAsync("tok").Returns(link);

        var repo = Substitute.For<ILeagueRepository>();
        repo.UserExistsInLeagueAsync(MemberId, 1).Returns(true);

        var controller = BuildController(BuildPrincipal(MemberId), repo: repo, linkService: linkService);

        var result = await controller.JoinViaLink("tok");

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task JoinViaLink_ReturnsNoContent_WhenSuccessful()
    {
        var link = new LeagueInviteLink { Token = "tok", LeagueId = 1, League = new LeagueInfo { Id = 1, LeagueName = "L" }, ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) };
        var linkService = Substitute.For<ILeagueInviteLinkService>();
        linkService.ValidateAsync("tok").Returns(link);

        var repo = Substitute.For<ILeagueRepository>();
        repo.UserExistsInLeagueAsync(MemberId, 1).Returns(false);

        var controller = BuildController(BuildPrincipal(MemberId), repo: repo, linkService: linkService);

        var result = await controller.JoinViaLink("tok");

        Assert.IsType<NoContentResult>(result);
        await repo.Received(1).AddLeagueUserMappingAsync(Arg.Is<LeagueUserMapping>(m =>
            m.LeagueId == 1 && m.UserId == MemberId));
    }
}
