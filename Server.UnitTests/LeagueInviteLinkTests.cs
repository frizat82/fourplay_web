using FourPlayWebApp.Server.Controllers;
using FourPlayWebApp.Server.Models;
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
        ILeagueInviteLinkService? linkService = null,
        IInvitationService? invitationService = null)
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
            invitationService ?? Substitute.For<IInvitationService>(),
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
        linkService.GenerateAsync(1, OwnerId).Returns(new LeagueInviteLink
        {
            Token = "abc123",
            LeagueId = 1,
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
        linkService.GenerateAsync(1, Arg.Any<string>()).Returns(new LeagueInviteLink
        {
            Token = "xyz789",
            LeagueId = 1,
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

    // ── GetCurrentInviteLink ─────────────────────────────────────────────────

    [Fact]
    public async Task GetCurrentInviteLink_ReturnsForbid_WhenCallerIsNotOwnerOrAdmin()
    {
        var repo = Substitute.For<ILeagueRepository>();
        repo.GetLeagueInfoAsync(1).Returns(new LeagueInfo { Id = 1, OwnerUserId = OwnerId, LeagueName = "TestLeague" });

        var controller = BuildController(BuildPrincipal(StrangerId), repo: repo);

        var result = await controller.GetCurrentInviteLink(1);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task GetCurrentInviteLink_ReturnsNotFound_WhenNoLinkExists()
    {
        var repo = Substitute.For<ILeagueRepository>();
        repo.GetLeagueInfoAsync(1).Returns(new LeagueInfo { Id = 1, OwnerUserId = OwnerId, LeagueName = "TestLeague" });

        var linkService = Substitute.For<ILeagueInviteLinkService>();
        linkService.GetCurrentAsync(1).Returns((LeagueInviteLink?)null);

        var controller = BuildController(BuildPrincipal(OwnerId), repo: repo, linkService: linkService);

        var result = await controller.GetCurrentInviteLink(1);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetCurrentInviteLink_ReturnsOk_WithDto_WhenLinkExists()
    {
        var repo = Substitute.For<ILeagueRepository>();
        repo.GetLeagueInfoAsync(1).Returns(new LeagueInfo { Id = 1, OwnerUserId = OwnerId, LeagueName = "TestLeague" });

        var linkService = Substitute.For<ILeagueInviteLinkService>();
        var expires = DateTimeOffset.UtcNow.AddHours(20);
        linkService.GetCurrentAsync(1).Returns(new LeagueInviteLink { Token = "abc", LeagueId = 1, ExpiresAt = expires });

        var controller = BuildController(BuildPrincipal(OwnerId), repo: repo, linkService: linkService);

        var result = await controller.GetCurrentInviteLink(1);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<LeagueInviteLinkDto>(ok.Value);
        Assert.Equal("abc", dto.Token);
        Assert.Equal("TestLeague", dto.LeagueName);
        Assert.Equal(expires, dto.ExpiresAt);
    }

    [Fact]
    public async Task GetCurrentInviteLink_ReturnsOk_WhenCallerIsAdmin()
    {
        var repo = Substitute.For<ILeagueRepository>();
        repo.GetLeagueInfoAsync(1).Returns(new LeagueInfo { Id = 1, OwnerUserId = OwnerId, LeagueName = "TestLeague" });

        var linkService = Substitute.For<ILeagueInviteLinkService>();
        linkService.GetCurrentAsync(1).Returns(new LeagueInviteLink { Token = "tok", LeagueId = 1, ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) });

        var controller = BuildController(BuildPrincipal("admin-user", isAdmin: true), repo: repo, linkService: linkService);

        var result = await controller.GetCurrentInviteLink(1);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    // ── RevokeInviteLink ─────────────────────────────────────────────────────

    [Fact]
    public async Task RevokeInviteLink_ReturnsNotFound_WhenLeagueDoesNotExist()
    {
        var repo = Substitute.For<ILeagueRepository>();
        repo.GetLeagueInfoAsync(999).Returns(Task.FromException<LeagueInfo>(new InvalidOperationException("Sequence contains no elements")));

        var controller = BuildController(BuildPrincipal(OwnerId), repo: repo);

        var result = await controller.RevokeInviteLink(999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task RevokeInviteLink_ReturnsForbid_WhenCallerIsNotOwnerOrAdmin()
    {
        var repo = Substitute.For<ILeagueRepository>();
        repo.GetLeagueInfoAsync(1).Returns(new LeagueInfo { Id = 1, OwnerUserId = OwnerId, LeagueName = "TestLeague" });

        var linkService = Substitute.For<ILeagueInviteLinkService>();
        var controller = BuildController(BuildPrincipal(StrangerId), repo: repo, linkService: linkService);

        var result = await controller.RevokeInviteLink(1);

        Assert.IsType<ForbidResult>(result);
        await linkService.DidNotReceive().RevokeAsync(Arg.Any<int>());
    }

    [Fact]
    public async Task RevokeInviteLink_ReturnsNoContent_WhenCallerIsOwner()
    {
        var repo = Substitute.For<ILeagueRepository>();
        repo.GetLeagueInfoAsync(1).Returns(new LeagueInfo { Id = 1, OwnerUserId = OwnerId, LeagueName = "TestLeague" });

        var linkService = Substitute.For<ILeagueInviteLinkService>();
        var controller = BuildController(BuildPrincipal(OwnerId), repo: repo, linkService: linkService);

        var result = await controller.RevokeInviteLink(1);

        Assert.IsType<NoContentResult>(result);
        await linkService.Received(1).RevokeAsync(1);
    }

    [Fact]
    public async Task RevokeInviteLink_ReturnsNoContent_WhenCallerIsAdmin()
    {
        var repo = Substitute.For<ILeagueRepository>();
        repo.GetLeagueInfoAsync(1).Returns(new LeagueInfo { Id = 1, OwnerUserId = OwnerId, LeagueName = "TestLeague" });

        var linkService = Substitute.For<ILeagueInviteLinkService>();
        var controller = BuildController(BuildPrincipal("admin-user", isAdmin: true), repo: repo, linkService: linkService);

        var result = await controller.RevokeInviteLink(1);

        Assert.IsType<NoContentResult>(result);
    }

    // ── GetLeagueInvitations ─────────────────────────────────────────────────

    [Fact]
    public async Task GetLeagueInvitations_ReturnsForbid_WhenCallerIsNotOwnerOrAdmin()
    {
        var repo = Substitute.For<ILeagueRepository>();
        repo.GetLeagueInfoAsync(1).Returns(new LeagueInfo { Id = 1, OwnerUserId = OwnerId, LeagueName = "TestLeague" });

        var controller = BuildController(BuildPrincipal(StrangerId), repo: repo);

        var result = await controller.GetLeagueInvitations(1);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task GetLeagueInvitations_ReturnsOk_WithMappedDtos()
    {
        var repo = Substitute.For<ILeagueRepository>();
        repo.GetLeagueInfoAsync(1).Returns(new LeagueInfo { Id = 1, OwnerUserId = OwnerId, LeagueName = "TestLeague" });

        var invSvc = Substitute.For<IInvitationService>();
        invSvc.GetInvitationsByLeagueAsync(1).Returns(new List<Invitation>
        {
            new() { Id = 10, Email = "alice@test.com", LeagueId = 1,
                    CreatedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
                    IsUsed = false }
        });

        var controller = BuildController(BuildPrincipal(OwnerId), repo: repo, invitationService: invSvc);

        var result = await controller.GetLeagueInvitations(1);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IEnumerable<InvitationDto>>(ok.Value);
        var item = Assert.Single(list);
        Assert.Equal("alice@test.com", item.Email);
        Assert.Equal(10, item.Id);
    }

    [Fact]
    public async Task GetLeagueInvitations_UsedInvitation_CarriesRegisteredUserEmailConfirmed()
    {
        // Mirrors the admin Invitations page fix — owners must see the same "used but not yet
        // confirmed" distinction, not just a blanket "Accepted" that hides the same stuck-user
        // scenario this whole fix was written to resolve.
        var repo = Substitute.For<ILeagueRepository>();
        repo.GetLeagueInfoAsync(1).Returns(new LeagueInfo { Id = 1, OwnerUserId = OwnerId, LeagueName = "TestLeague" });

        var invSvc = Substitute.For<IInvitationService>();
        invSvc.GetInvitationsByLeagueAsync(1).Returns(new List<Invitation>
        {
            new() { Id = 10, Email = "alice@test.com", LeagueId = 1, IsUsed = true,
                    RegisteredUserId = "alice-1",
                    RegisteredUser = new ApplicationUser { UserName = "alice", EmailConfirmed = false } }
        });

        var controller = BuildController(BuildPrincipal(OwnerId), repo: repo, invitationService: invSvc);

        var result = await controller.GetLeagueInvitations(1);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var item = Assert.Single(Assert.IsAssignableFrom<IEnumerable<InvitationDto>>(ok.Value));
        Assert.False(item.RegisteredUserEmailConfirmed);
    }

    [Fact]
    public async Task GetLeagueInvitations_ReturnsOk_WhenCallerIsAdmin()
    {
        var repo = Substitute.For<ILeagueRepository>();
        repo.GetLeagueInfoAsync(1).Returns(new LeagueInfo { Id = 1, OwnerUserId = OwnerId, LeagueName = "TestLeague" });

        var invSvc = Substitute.For<IInvitationService>();
        invSvc.GetInvitationsByLeagueAsync(1).Returns(new List<Invitation>());

        var controller = BuildController(BuildPrincipal("admin-user", isAdmin: true), repo: repo, invitationService: invSvc);

        var result = await controller.GetLeagueInvitations(1);

        Assert.IsType<OkObjectResult>(result.Result);
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
