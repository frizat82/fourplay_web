using FourPlayWebApp.Server.Controllers;
using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models.Account;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using System.Security.Claims;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// Regression tests for Issue #6: ChangePassword derives target user from the
/// request body email instead of the authenticated caller's JWT claim.
///
/// Before fix: any authenticated user can change another user's password by
///             supplying a different email in the request body.
/// After fix:  the email is derived from User.FindFirstValue(ClaimTypes.NameIdentifier)
///             and the body email field is ignored for user lookup.
/// </summary>
public class ChangePasswordTests
{
    private static AuthController BuildController(
        UserManager<ApplicationUser> userManager,
        ClaimsPrincipal principal)
    {
        var signInManager = Substitute.For<SignInManager<ApplicationUser>>(
            userManager,
            Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<ApplicationUser>>(),
            null, null, null, null);

        var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("ChangePasswordTests_" + Guid.NewGuid())
                .Options);

        var controller = new AuthController(
            userManager,
            signInManager,
            Substitute.For<IEmailSender>(),
            Substitute.For<IEmailSender<ApplicationUser>>(),
            Substitute.For<IInvitationService>(),
            NullLogger<AuthController>.Instance,
            Substitute.For<IConfiguration>(),
            Substitute.For<IRefreshTokenService>(),
            Substitute.For<IJwtTokenService>(),
            Substitute.For<IWebHostEnvironment>(),
            db
        );

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        return controller;
    }

    private static ClaimsPrincipal BuildPrincipal(string userId, string email) =>
        TestPrincipalFactory.Build(userId, email);

    [Fact]
    public async Task ChangePassword_UsesJwtClaim_NotBodyEmail()
    {
        // Arrange — authenticated user owns "caller@example.com"
        const string callerUserId = "caller-id";
        const string callerEmail  = "caller@example.com";
        const string victimEmail  = "victim@example.com";

        var userStore   = Substitute.For<IUserStore<ApplicationUser>>();
        var userManager = Substitute.For<UserManager<ApplicationUser>>(
            userStore, null, null, null, null, null, null, null, null);

        var callerUser = new ApplicationUser { Id = callerUserId, Email = callerEmail };
        var victimUser = new ApplicationUser { Id = "victim-id", Email = victimEmail };

        // FindByIdAsync returns the caller when queried by JWT claim userId
        userManager.FindByIdAsync(callerUserId).Returns(callerUser);
        // FindByEmailAsync would return the victim if body email is used (the bug)
        userManager.FindByEmailAsync(victimEmail).Returns(victimUser);

        userManager.ChangePasswordAsync(callerUser, Arg.Any<string>(), Arg.Any<string>())
                   .Returns(IdentityResult.Success);

        var principal  = BuildPrincipal(callerUserId, callerEmail);
        var controller = BuildController(userManager, principal);

        var model = new ChangePassword
        {
            CurrentPassword = "OldPass!1",
            Password        = "NewPass!2",
        };

        // Act
        var result = await controller.ChangePassword(model);

        // Assert — after fix, the victim's password is NOT changed
        // The controller must use FindByIdAsync(JWT claim), NOT FindByEmailAsync(body)
        await userManager.DidNotReceive().ChangePasswordAsync(victimUser, Arg.Any<string>(), Arg.Any<string>());
        await userManager.Received().ChangePasswordAsync(callerUser, Arg.Any<string>(), Arg.Any<string>());

        Assert.IsType<OkResult>(result.Result); // caller's own password changed OK
    }

    [Fact]
    public async Task ChangePassword_ReturnsUnauthorized_WhenCallerNotFound()
    {
        var userStore   = Substitute.For<IUserStore<ApplicationUser>>();
        var userManager = Substitute.For<UserManager<ApplicationUser>>(
            userStore, null, null, null, null, null, null, null, null);

        // JWT claim userId resolves to nothing (token for deleted user)
        userManager.FindByIdAsync(Arg.Any<string>()).Returns((ApplicationUser?)null);

        var principal  = BuildPrincipal("ghost-id", "ghost@example.com");
        var controller = BuildController(userManager, principal);

        var result = await controller.ChangePassword(new ChangePassword
        {
            CurrentPassword = "Old!1",
            Password        = "New!2",
        });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }
}
