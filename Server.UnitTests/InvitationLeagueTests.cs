using FourPlayWebApp.Server.Controllers;
using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Models;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models.Account;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// TDD tests for league-aware invitations (frizat-ecj).
/// All tests are written RED first — they will fail until implementation is added.
/// </summary>
public class InvitationLeagueTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ApplicationDbContext BuildDb(string name) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name)
            .Options);

    private static IDbContextFactory<ApplicationDbContext> BuildFactory(ApplicationDbContext db)
    {
        var factory = Substitute.For<IDbContextFactory<ApplicationDbContext>>();
        factory.CreateDbContextAsync().Returns(db);
        return factory;
    }

    private static UserManager<ApplicationUser> BuildUserManager()
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        var mgr = Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);
        mgr.GetAccessFailedCountAsync(Arg.Any<ApplicationUser>()).Returns(0);
        return mgr;
    }

    private static IWebHostEnvironment BuildDevEnv()
    {
        var env = Substitute.For<IWebHostEnvironment>();
        env.EnvironmentName.Returns("Development");
        return env;
    }

    // ── InvitationService tests ───────────────────────────────────────────────

    [Fact]
    public async Task CreateInvitation_StoresLeagueId_WhenProvided()
    {
        var db = BuildDb(nameof(CreateInvitation_StoresLeagueId_WhenProvided));
        var service = new InvitationService(BuildFactory(db));

        var result = await service.CreateInvitationAsync("user@example.com", "admin-1", leagueId: 42);

        Assert.Equal(42, result.LeagueId);
    }

    [Fact]
    public async Task CreateInvitation_NullLeagueId_WhenNotProvided()
    {
        var db = BuildDb(nameof(CreateInvitation_NullLeagueId_WhenNotProvided));
        var service = new InvitationService(BuildFactory(db));

        var result = await service.CreateInvitationAsync("user@example.com", "admin-1");

        Assert.Null(result.LeagueId);
    }

    [Fact]
    public async Task ValidateInvitation_ReturnsLeagueId_WhenLeagueAssigned()
    {
        var db = BuildDb(nameof(ValidateInvitation_ReturnsLeagueId_WhenLeagueAssigned));
        db.Invitations.Add(new Invitation
        {
            InvitationCode = "test-code-123",
            Email = "user@example.com",
            LeagueId = 7,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
        });
        await db.SaveChangesAsync();

        var service = new InvitationService(BuildFactory(db));
        var result = await service.ValidateInvitationAsync("test-code-123");

        Assert.NotNull(result);
        Assert.Equal(7, result.LeagueId);
    }

    // ── AuthController tests ──────────────────────────────────────────────────

    [Fact]
    public async Task CreateUser_AutoAssignsLeague_WhenInvitationHasLeagueId()
    {
        // Arrange
        var db = BuildDb(nameof(CreateUser_AutoAssignsLeague_WhenInvitationHasLeagueId));
        var userManager = BuildUserManager();
        var invitationService = Substitute.For<IInvitationService>();
        var config = Substitute.For<IConfiguration>();

        var invitation = new Invitation
        {
            Id = 1,
            InvitationCode = "code-abc",
            Email = "newuser@example.com",
            LeagueId = 5,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
        };

        invitationService.ValidateInvitationAsync("code-abc").Returns(invitation);
        invitationService.MarkInvitationAsUsedAsync("code-abc", Arg.Any<string>()).Returns(true);

        userManager.FindByEmailAsync("newuser@example.com").Returns((ApplicationUser?)null);
        userManager.CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(IdentityResult.Success);
        userManager.GenerateEmailConfirmationTokenAsync(Arg.Any<ApplicationUser>())
            .Returns("token");

        config["App:BaseUrl"].Returns("http://localhost");

        var controller = BuildAuthController(userManager, invitationService, config, db);

        // Act
        var result = await controller.CreateUser(new CreateUserRequest
        {
            Username = "newuser",
            Email = "newuser@example.com",
            Password = "Pass@123",
            Code = "code-abc",
        });

        // Assert — LeagueUserMapping must exist for the new user in league 5
        var mapping = db.LeagueUserMapping.FirstOrDefault(m => m.LeagueId == 5);
        Assert.NotNull(mapping);
    }

    [Fact]
    public async Task CreateUser_NoLeagueAssignment_WhenInvitationHasNoLeagueId()
    {
        // Arrange
        var db = BuildDb(nameof(CreateUser_NoLeagueAssignment_WhenInvitationHasNoLeagueId));
        var userManager = BuildUserManager();
        var invitationService = Substitute.For<IInvitationService>();
        var config = Substitute.For<IConfiguration>();

        var invitation = new Invitation
        {
            Id = 2,
            InvitationCode = "code-xyz",
            Email = "user2@example.com",
            LeagueId = null,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
        };

        invitationService.ValidateInvitationAsync("code-xyz").Returns(invitation);
        invitationService.MarkInvitationAsUsedAsync("code-xyz", Arg.Any<string>()).Returns(true);

        userManager.FindByEmailAsync("user2@example.com").Returns((ApplicationUser?)null);
        userManager.CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(IdentityResult.Success);
        userManager.GenerateEmailConfirmationTokenAsync(Arg.Any<ApplicationUser>())
            .Returns("token");

        config["App:BaseUrl"].Returns("http://localhost");

        var controller = BuildAuthController(userManager, invitationService, config, db);

        // Act
        await controller.CreateUser(new CreateUserRequest
        {
            Username = "user2",
            Email = "user2@example.com",
            Password = "Pass@123",
            Code = "code-xyz",
        });

        // Assert — no LeagueUserMapping should be created
        Assert.Empty(db.LeagueUserMapping);
    }

    // ── Builder ───────────────────────────────────────────────────────────────

    private static AuthController BuildAuthController(
        UserManager<ApplicationUser> userManager,
        IInvitationService invitationService,
        IConfiguration config,
        ApplicationDbContext db)
    {
        var signInManager = Substitute.For<SignInManager<ApplicationUser>>(
            userManager,
            Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<ApplicationUser>>(),
            null, null, null, null);

        var controller = new AuthController(
            userManager,
            signInManager,
            Substitute.For<IEmailSender>(),
            Substitute.For<IEmailSender<ApplicationUser>>(),
            invitationService,
            NullLogger<AuthController>.Instance,
            config,
            Substitute.For<IRefreshTokenService>(),
            Substitute.For<IJwtTokenService>(),
            BuildDevEnv(),
            db
        );

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };

        return controller;
    }
}
