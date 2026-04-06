using FourPlayWebApp.Server.Controllers;
using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models.Account;
using FourPlayWebApp.Shared.Models.Email;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using System.Security.Claims;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// Tests covering identified bugs in the email process:
///
/// Bug 1 — InvitationController.SendPasswordResetLink calls SendPasswordResetCodeAsync
///          instead of SendPasswordResetLinkAsync, sending wrong email template.
///          Tests marked [Bug1] are RED until fixed.
///
/// Bug 2 — AuthController.CreateUser does not send confirmation email server-side.
///          Client must make a second separate HTTP call which can fail silently,
///          leaving users unable to log in.
///          Tests marked [Bug2] are RED until fixed.
/// </summary>
public class EmailProcessTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static InvitationController BuildInvitationController(
        IEmailSender emailSender,
        IEmailSender<ApplicationUser> emailSenderApp)
    {
        var invitationService = Substitute.For<IInvitationService>();
        return new InvitationController(invitationService, emailSender, emailSenderApp);
    }

    private static AuthController BuildAuthController(
        UserManager<ApplicationUser> userManager,
        IEmailSender<ApplicationUser> emailSenderApp,
        IInvitationService invitationService)
    {
        var signInManager = Substitute.For<SignInManager<ApplicationUser>>(
            userManager,
            Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<ApplicationUser>>(),
            null, null, null, null);

        var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("EmailProcessTests_" + Guid.NewGuid())
                .Options);

        var controller = new AuthController(
            userManager,
            signInManager,
            Substitute.For<IEmailSender>(),
            emailSenderApp,
            invitationService,
            NullLogger<AuthController>.Instance,
            Substitute.For<IConfiguration>(),
            Substitute.For<IRefreshTokenService>(),
            Substitute.For<IJwtTokenService>(),
            Substitute.For<IWebHostEnvironment>(),
            db
        );

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity())
            }
        };

        return controller;
    }

    // -------------------------------------------------------------------------
    // Bug 1: send-reset-link calls wrong IEmailSender method
    // -------------------------------------------------------------------------

    /// <summary>
    /// [Bug 1 - RED until fixed]
    /// InvitationController.SendPasswordResetLink must call SendPasswordResetLinkAsync,
    /// not SendPasswordResetCodeAsync. Wrong method sends a code-style email rather
    /// than the expected clickable-link email.
    /// </summary>
    [Fact]
    public async Task SendResetLink_CallsSendPasswordResetLinkAsync_NotCodeAsync()
    {
        var emailSenderApp = Substitute.For<IEmailSender<ApplicationUser>>();
        var controller = BuildInvitationController(Substitute.For<IEmailSender>(), emailSenderApp);

        var request = new PasswordResetLinkRequest("testuser", "user@example.com", "https://app.example.com/reset?code=abc123");

        await controller.SendPasswordResetLink(request);

        // Should call the LINK method — will be RED until fixed
        await emailSenderApp.Received(1).SendPasswordResetLinkAsync(
            Arg.Is<ApplicationUser>(u => u.UserName == "testuser"),
            "user@example.com",
            "https://app.example.com/reset?code=abc123");
    }

    /// <summary>
    /// [Bug 1 - RED until fixed]
    /// The code (SendPasswordResetCodeAsync) method must NOT be called from send-reset-link.
    /// </summary>
    [Fact]
    public async Task SendResetLink_DoesNotCallSendPasswordResetCodeAsync()
    {
        var emailSenderApp = Substitute.For<IEmailSender<ApplicationUser>>();
        var controller = BuildInvitationController(Substitute.For<IEmailSender>(), emailSenderApp);

        var request = new PasswordResetLinkRequest("testuser", "user@example.com", "https://app.example.com/reset?code=abc123");

        await controller.SendPasswordResetLink(request);

        // Must NOT call the code method — will be RED until fixed
        await emailSenderApp.DidNotReceive().SendPasswordResetCodeAsync(
            Arg.Any<ApplicationUser>(),
            Arg.Any<string>(),
            Arg.Any<string>());
    }

    /// <summary>
    /// Sanity check: send-reset-code must still call SendPasswordResetCodeAsync (not broken by fix).
    /// This test should always be GREEN.
    /// </summary>
    [Fact]
    public async Task SendResetCode_CallsSendPasswordResetCodeAsync()
    {
        var emailSenderApp = Substitute.For<IEmailSender<ApplicationUser>>();
        var controller = BuildInvitationController(Substitute.For<IEmailSender>(), emailSenderApp);

        var request = new PasswordResetCodeRequest("testuser", "user@example.com", "123456");

        await controller.SendPasswordResetCode(request);

        await emailSenderApp.Received(1).SendPasswordResetCodeAsync(
            Arg.Is<ApplicationUser>(u => u.UserName == "testuser"),
            "user@example.com",
            "123456");
    }

    // -------------------------------------------------------------------------
    // Bug 2: create-user does not send confirmation email server-side
    // -------------------------------------------------------------------------

    /// <summary>
    /// [Bug 2 - RED until fixed]
    /// After CreateUser succeeds, the server must send a confirmation email directly.
    /// Currently the server does not send it — it relies on the client making a
    /// separate /request-email-confirmation call, which can silently fail and leave
    /// users unable to log in (RequireConfirmedEmail = true).
    /// </summary>
    [Fact]
    public async Task CreateUser_WhenSuccessful_SendsConfirmationEmailServerSide()
    {
        // Arrange
        const string email = "newuser@example.com";
        const string username = "newuser";
        const string inviteCode = "validcode123";

        var invitation = new FourPlayWebApp.Server.Models.Invitation
        {
            Email = email,
            InvitationCode = inviteCode,
            IsUsed = false,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        var invitationService = Substitute.For<IInvitationService>();
        invitationService.ValidateInvitationAsync(inviteCode).Returns(invitation);
        invitationService.MarkInvitationAsUsedAsync(inviteCode, Arg.Any<string>()).Returns(true);

        var userStore = Substitute.For<IUserStore<ApplicationUser>>();
        var userManager = Substitute.For<UserManager<ApplicationUser>>(
            userStore, null, null, null, null, null, null, null, null);

        var createdUser = new ApplicationUser { Id = "new-user-id", UserName = username, Email = email };

        userManager.FindByEmailAsync(email).Returns((ApplicationUser?)null);
        userManager.CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
                   .Returns(IdentityResult.Success);
        // Simulate Identity setting the Id on the created user
        userManager.Users.Returns(new[] { createdUser }.AsQueryable());
        userManager.GenerateEmailConfirmationTokenAsync(Arg.Any<ApplicationUser>())
                   .Returns("confirmation-token");

        var emailSenderApp = Substitute.For<IEmailSender<ApplicationUser>>();
        var controller = BuildAuthController(userManager, emailSenderApp, invitationService);

        var request = new CreateUserRequest
        {
            Email = email,
            Username = username,
            Password = "Test@12345",
            Code = inviteCode
        };

        // Act
        await controller.CreateUser(request);

        // Assert — confirmation email must be sent server-side (RED until fixed)
        await emailSenderApp.Received(1).SendConfirmationLinkAsync(
            Arg.Any<ApplicationUser>(),
            email,
            Arg.Any<string>());
    }

    /// <summary>
    /// [Bug 2 - RED until fixed]
    /// If confirmation email sending fails, the user should still be created successfully.
    /// Email failure must not cause create-user to return an error response.
    /// </summary>
    [Fact]
    public async Task CreateUser_WhenConfirmationEmailFails_UserStillCreatedSuccessfully()
    {
        // Arrange
        const string email = "newuser2@example.com";
        const string inviteCode = "validcode456";

        var invitation = new FourPlayWebApp.Server.Models.Invitation
        {
            Email = email,
            InvitationCode = inviteCode,
            IsUsed = false,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        var invitationService = Substitute.For<IInvitationService>();
        invitationService.ValidateInvitationAsync(inviteCode).Returns(invitation);
        invitationService.MarkInvitationAsUsedAsync(inviteCode, Arg.Any<string>()).Returns(true);

        var userStore = Substitute.For<IUserStore<ApplicationUser>>();
        var userManager = Substitute.For<UserManager<ApplicationUser>>(
            userStore, null, null, null, null, null, null, null, null);

        var createdUser = new ApplicationUser { Id = "new-user-id-2", UserName = "newuser2", Email = email };
        userManager.FindByEmailAsync(email).Returns((ApplicationUser?)null);
        userManager.CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
                   .Returns(IdentityResult.Success);
        userManager.Users.Returns(new[] { createdUser }.AsQueryable());
        userManager.GenerateEmailConfirmationTokenAsync(Arg.Any<ApplicationUser>())
                   .Returns("confirmation-token");

        var emailSenderApp = Substitute.For<IEmailSender<ApplicationUser>>();
        // Email sender throws — user creation must still succeed
        emailSenderApp
            .SendConfirmationLinkAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromException(new InvalidOperationException("SMTP unavailable")));

        var controller = BuildAuthController(userManager, emailSenderApp, invitationService);

        var request = new CreateUserRequest
        {
            Email = email,
            Username = "newuser2",
            Password = "Test@12345",
            Code = inviteCode
        };

        // Act
        var result = await controller.CreateUser(request);

        // Assert — user creation succeeds even when email fails (RED until fixed)
        Assert.IsType<OkObjectResult>(result.Result);
    }
}
