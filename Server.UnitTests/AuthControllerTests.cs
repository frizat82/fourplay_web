using FourPlayWebApp.Server.Controllers;
using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models.Account;
using FourPlayWebApp.Shared.Models.Account.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using System.Security.Claims;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// Unit tests for the HTTP-layer behaviour of AuthController.
/// All dependencies are substituted with NSubstitute mocks; no test server is spun up.
/// </summary>
public class AuthControllerTests
{
    // ── Factories ────────────────────────────────────────────────────────────

    private static UserManager<ApplicationUser> BuildUserManager()
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        var mgr   = Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);
        // Reasonable defaults
        mgr.GetAccessFailedCountAsync(Arg.Any<ApplicationUser>()).Returns(0);
        return mgr;
    }

    private static SignInManager<ApplicationUser> BuildSignInManager(
        UserManager<ApplicationUser> userManager)
    {
        return Substitute.For<SignInManager<ApplicationUser>>(
            userManager,
            Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<ApplicationUser>>(),
            null, null, null, null);
    }

    private static IWebHostEnvironment BuildDevEnvironment()
    {
        // IsDevelopment() → true  → UseSecureCookies = false (avoids HTTPS cookie requirement)
        var env = Substitute.For<IWebHostEnvironment>();
        env.EnvironmentName.Returns("Development");
        return env;
    }

    /// <summary>
    /// Builds the controller with a DefaultHttpContext that has a real
    /// ResponseCookies implementation backed by the response headers.
    /// Optionally attaches a ClaimsPrincipal to the context.
    /// </summary>
    private static AuthController BuildController(
        UserManager<ApplicationUser>? userManager   = null,
        SignInManager<ApplicationUser>? signInManager = null,
        IJwtTokenService? jwtTokenService           = null,
        IRefreshTokenService? refreshTokenService   = null,
        ClaimsPrincipal? principal                  = null,
        IWebHostEnvironment? environment            = null)
    {
        userManager     ??= BuildUserManager();
        signInManager   ??= BuildSignInManager(userManager);
        jwtTokenService ??= Substitute.For<IJwtTokenService>();
        refreshTokenService ??= Substitute.For<IRefreshTokenService>();
        environment     ??= BuildDevEnvironment();

        var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("AuthControllerTests_" + Guid.NewGuid())
                .Options);

        var controller = new AuthController(
            userManager,
            signInManager,
            Substitute.For<IEmailSender>(),
            Substitute.For<IEmailSender<ApplicationUser>>(),
            Substitute.For<IInvitationService>(),
            NullLogger<AuthController>.Instance,
            Substitute.For<IConfiguration>(),
            refreshTokenService,
            jwtTokenService,
            environment,
            db
        );

        var httpContext = new DefaultHttpContext();
        if (principal is not null)
            httpContext.User = principal;

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        return controller;
    }

    private static ApplicationUser BuildUser(string id = "user-1", string userName = "testuser") =>
        new() { Id = id, UserName = userName, Email = $"{userName}@example.com" };

    private static ClaimsPrincipal BuildPrincipal(string userId) =>
        TestPrincipalFactory.Build(userId);

    // ── Test 1: User not found → same shape as wrong password (no enumeration) ──

    [Fact]
    public async Task Login_UserNotFound_ReturnsOk_WithSucceededFalse_AndInvalidCredentialsMessage()
    {
        var userManager = BuildUserManager();
        userManager.FindByNameAsync(Arg.Any<string>()).Returns((ApplicationUser?)null);
        userManager.FindByEmailAsync(Arg.Any<string>()).Returns((ApplicationUser?)null);

        var controller = BuildController(userManager: userManager);

        var result = await controller.Login(new LoginRequest
        {
            Username   = "nobody",
            Password   = "pass",
            RememberMe = false,
        });

        var ok  = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<SignInResultDto>(ok.Value);
        Assert.False(dto.Succeeded);
        Assert.Equal("Invalid credentials", dto.Message);
    }

    // ── Test 2: Wrong password → Ok with Succeeded=false ─────────────────────

    [Fact]
    public async Task Login_WrongPassword_ReturnsOk_WithSucceededFalse_AndInvalidCredentialsMessage()
    {
        var userManager   = BuildUserManager();
        var user          = BuildUser();

        userManager.FindByNameAsync(user.UserName!).Returns(user);
        userManager.GetAccessFailedCountAsync(user).Returns(1);

        var signInManager = BuildSignInManager(userManager);
        signInManager.PasswordSignInAsync(
                user,
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<bool>())
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Failed);

        var controller = BuildController(userManager: userManager, signInManager: signInManager);

        var result = await controller.Login(new LoginRequest
        {
            Username   = user.UserName!,
            Password   = "wrong-password",
            RememberMe = false,
        });

        var ok  = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<SignInResultDto>(ok.Value);

        Assert.False(dto.Succeeded);
        Assert.Equal("Invalid credentials", dto.Message);
    }

    // ── Test 3: Locked out → Ok with IsLockedOut=true ────────────────────────

    [Fact]
    public async Task Login_LockedOut_ReturnsOk_WithIsLockedOutTrue()
    {
        var userManager   = BuildUserManager();
        var user          = BuildUser();

        userManager.FindByNameAsync(user.UserName!).Returns(user);

        var signInManager = BuildSignInManager(userManager);
        signInManager.PasswordSignInAsync(
                user,
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<bool>())
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.LockedOut);

        var controller = BuildController(userManager: userManager, signInManager: signInManager);

        var result = await controller.Login(new LoginRequest
        {
            Username   = user.UserName!,
            Password   = "any",
            RememberMe = false,
        });

        var ok  = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<SignInResultDto>(ok.Value);

        Assert.True(dto.IsLockedOut);
    }

    // ── Test 4: Successful login → Ok with Succeeded=true and JWT generated ──

    [Fact]
    public async Task Login_Success_ReturnsOk_WithSucceededTrue_AndCallsJwtService()
    {
        var userManager   = BuildUserManager();
        var user          = BuildUser();

        userManager.FindByNameAsync(user.UserName!).Returns(user);

        var signInManager = BuildSignInManager(userManager);
        signInManager.PasswordSignInAsync(
                user,
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<bool>())
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Success);

        var jwtService = Substitute.For<IJwtTokenService>();
        jwtService.GenerateAccessTokenAsync(user, Arg.Any<bool>())
                  .Returns(("fake.jwt.token", DateTime.UtcNow.AddHours(1)));

        var controller = BuildController(
            userManager:     userManager,
            signInManager:   signInManager,
            jwtTokenService: jwtService);

        var result = await controller.Login(new LoginRequest
        {
            Username   = user.UserName!,
            Password   = "correct-password",
            RememberMe = false,
        });

        var ok  = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<SignInResultDto>(ok.Value);

        Assert.True(dto.Succeeded);
        Assert.Equal("Login successful", dto.Message);

        // Verify JWT service was called
        await jwtService.Received(1).GenerateAccessTokenAsync(user, Arg.Any<bool>());
    }

    // ── Test 5: Successful login → AuthToken cookie is set ───────────────────

    [Fact]
    public async Task Login_Success_SetsAuthTokenCookie()
    {
        var userManager   = BuildUserManager();
        var user          = BuildUser();

        userManager.FindByNameAsync(user.UserName!).Returns(user);

        var signInManager = BuildSignInManager(userManager);
        signInManager.PasswordSignInAsync(
                user,
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<bool>())
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Success);

        const string fakeJwt = "fake.jwt.token";
        var jwtService = Substitute.For<IJwtTokenService>();
        jwtService.GenerateAccessTokenAsync(user, Arg.Any<bool>())
                  .Returns((fakeJwt, DateTime.UtcNow.AddHours(1)));

        var controller = BuildController(
            userManager:     userManager,
            signInManager:   signInManager,
            jwtTokenService: jwtService);

        await controller.Login(new LoginRequest
        {
            Username   = user.UserName!,
            Password   = "correct-password",
            RememberMe = false,
        });

        // DefaultHttpContext writes cookies to Set-Cookie response headers
        var setCookieHeaders = controller.HttpContext.Response.Headers["Set-Cookie"];

        Assert.True(setCookieHeaders.Count > 0, "Expected at least one Set-Cookie header");

        var authTokenCookie = setCookieHeaders
            .FirstOrDefault(h => h is not null && h.StartsWith("AuthToken="));

        Assert.NotNull(authTokenCookie);
        Assert.Contains(fakeJwt, authTokenCookie);
    }

    // ── Test 6: ChangePassword — missing NameIdentifier claim → Unauthorized ─
    // Note: The controller returns BadRequest (not Unauthorized) when FindByIdAsync
    // returns null. The missing-claim path hits FindByIdAsync(null!) which also
    // returns null → BadRequest. This test documents the actual behaviour.

    [Fact]
    public async Task ChangePassword_MissingUserIdClaim_ReturnsBadRequest()
    {
        var userManager = BuildUserManager();

        // No NameIdentifier claim in context → User.FindFirstValue returns null
        // → FindByIdAsync(null!) → returns null → BadRequest
        userManager.FindByIdAsync(Arg.Any<string>()).Returns((ApplicationUser?)null);

        // Principal with NO NameIdentifier claim
        var emptyPrincipal = new ClaimsPrincipal(new ClaimsIdentity());

        var controller = BuildController(
            userManager: userManager,
            principal:   emptyPrincipal);

        var result = await controller.ChangePassword(new ChangePassword
        {
            CurrentPassword = "Old!1",
            Password        = "New!2",
        });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // ── Bonus Test 7: FindByName returns null, FindByEmail also null → same as wrong password ─

    [Fact]
    public async Task Login_BothLookupsFail_ReturnsOk_WithSucceededFalse()
    {
        var userManager = BuildUserManager();
        userManager.FindByNameAsync(Arg.Any<string>()).Returns((ApplicationUser?)null);
        userManager.FindByEmailAsync(Arg.Any<string>()).Returns((ApplicationUser?)null);

        var controller = BuildController(userManager: userManager);

        var result = await controller.Login(new LoginRequest
        {
            Username   = "notfound@example.com",
            Password   = "pass",
            RememberMe = false,
        });

        var ok  = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<SignInResultDto>(ok.Value);
        Assert.False(dto.Succeeded);
        Assert.Equal("Invalid credentials", dto.Message);
    }

    // ── Bonus Test 8: Successful login with RememberMe=true issues refresh token ─

    [Fact]
    public async Task Login_SuccessWithRememberMe_IssuedRefreshToken()
    {
        var userManager   = BuildUserManager();
        var user          = BuildUser();

        userManager.FindByNameAsync(user.UserName!).Returns(user);

        var signInManager = BuildSignInManager(userManager);
        signInManager.PasswordSignInAsync(
                user,
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<bool>())
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Success);

        var jwtService = Substitute.For<IJwtTokenService>();
        jwtService.GenerateAccessTokenAsync(user, Arg.Any<bool>())
                  .Returns(("fake.jwt.token", DateTime.UtcNow.AddHours(1)));

        var fakeRefreshToken = new FourPlayWebApp.Server.Models.Identity.RefreshToken
        {
            Token   = "refresh-abc",
            UserId  = user.Id,
            Expires = DateTime.UtcNow.AddDays(14),
        };

        var refreshService = Substitute.For<IRefreshTokenService>();
        refreshService.IssueTokenAsync(user, Arg.Any<TimeSpan>())
                      .Returns(fakeRefreshToken);

        var controller = BuildController(
            userManager:        userManager,
            signInManager:      signInManager,
            jwtTokenService:    jwtService,
            refreshTokenService: refreshService);

        var result = await controller.Login(new LoginRequest
        {
            Username   = user.UserName!,
            Password   = "correct-password",
            RememberMe = true,
        });

        var ok  = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<SignInResultDto>(ok.Value);
        Assert.True(dto.Succeeded);

        // Refresh token service must have been called
        await refreshService.Received(1).IssueTokenAsync(user, Arg.Any<TimeSpan>());

        // RefreshToken cookie must be in response headers
        var setCookieHeaders = controller.HttpContext.Response.Headers["Set-Cookie"];
        var refreshCookie = setCookieHeaders
            .FirstOrDefault(h => h is not null && h.StartsWith("RefreshToken="));
        Assert.NotNull(refreshCookie);
    }

    /// <summary>
    /// frizat-8n3: ForgotPassword must return 200 OK even when the email is not found.
    /// Returning 400 leaks whether an account exists for that email (user enumeration).
    /// </summary>
    [Fact]
    public async Task ForgotPassword_UnknownEmail_ReturnsOk()
    {
        var userManager = BuildUserManager();
        userManager.FindByEmailAsync(Arg.Any<string>()).Returns((ApplicationUser?)null);

        var controller = BuildController(userManager: userManager);

        var result = await controller.ForgotPassword(new ForgotPasswordRequest
        {
            Email    = "nobody@example.com",
            ResetUrl = "https://example.com/reset",
        });

        // Must return 200, not 400 — leaking user existence is a security issue
        Assert.IsType<OkResult>(result.Result);
    }

    /// <summary>
    /// frizat-8n3: ResetPassword must return 200 OK even when the email is not found.
    /// Returning 400 leaks whether an account exists for that email (user enumeration).
    /// </summary>
    [Fact]
    public async Task ResetPassword_UnknownEmail_ReturnsOk()
    {
        var userManager = BuildUserManager();
        userManager.FindByEmailAsync(Arg.Any<string>()).Returns((ApplicationUser?)null);

        var controller = BuildController(userManager: userManager);

        var result = await controller.ResetPassword(new ResetPasswordRequest
        {
            Email    = "nobody@example.com",
            Token    = "sometoken",
            Password = "NewPass1!",
        });

        // Must return 200, not 400 — leaking user existence is a security issue
        Assert.IsType<OkResult>(result.Result);
    }

    // ── Rate limiter attribute coverage ──────────────────────────────────────

    [Fact]
    public void ResetPassword_HasRateLimitingAttribute()
    {
        var method = typeof(AuthController).GetMethod("ResetPassword");
        Assert.NotNull(method);
        var attr = method!.GetCustomAttributes(
            typeof(Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute), inherit: false)
            .Cast<Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute>()
            .SingleOrDefault();
        Assert.NotNull(attr);
        Assert.Equal("forgot", attr!.PolicyName);
    }

    [Fact]
    public void RequestEmailConfirmation_HasRateLimitingAttribute()
    {
        var method = typeof(AuthController).GetMethod("RequestEmailConfirmation");
        Assert.NotNull(method);
        var attr = method!.GetCustomAttributes(
            typeof(Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute), inherit: false)
            .Cast<Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute>()
            .SingleOrDefault();
        Assert.NotNull(attr);
        Assert.Equal("forgot", attr!.PolicyName);
    }

    // ── mon.7: Token revocation on password change/reset ──────────────────────

    [Fact]
    public async Task ChangePassword_Success_RevokesAllUserTokens()
    {
        const string userId = "user-99";
        var user        = BuildUser(id: userId);
        var userManager = BuildUserManager();
        userManager.FindByIdAsync(userId).Returns(user);
        userManager.ChangePasswordAsync(user, Arg.Any<string>(), Arg.Any<string>())
                   .Returns(IdentityResult.Success);

        var refreshService = Substitute.For<IRefreshTokenService>();
        refreshService.IssueTokenAsync(user, Arg.Any<TimeSpan>())
                      .Returns(new FourPlayWebApp.Server.Models.Identity.RefreshToken
                      {
                          Token = "new-refresh", UserId = userId,
                          Expires = DateTimeOffset.UtcNow.AddDays(14)
                      });

        var jwtService = Substitute.For<IJwtTokenService>();
        jwtService.GenerateAccessTokenAsync(user, Arg.Any<bool>())
                  .Returns(("new.jwt", DateTime.UtcNow.AddHours(1)));

        var controller = BuildController(
            userManager: userManager,
            refreshTokenService: refreshService,
            jwtTokenService: jwtService,
            principal: BuildPrincipal(userId));

        await controller.ChangePassword(new ChangePassword
        {
            CurrentPassword = "Old!1", Password = "New!2"
        });

        await refreshService.Received(1).RevokeAllUserTokensAsync(userId);
    }

    [Fact]
    public async Task ChangePassword_Success_ReissuesNewTokenSoCallerStaysLoggedIn()
    {
        const string userId = "user-99";
        var user        = BuildUser(id: userId);
        var userManager = BuildUserManager();
        userManager.FindByIdAsync(userId).Returns(user);
        userManager.ChangePasswordAsync(user, Arg.Any<string>(), Arg.Any<string>())
                   .Returns(IdentityResult.Success);

        var fakeRefresh = new FourPlayWebApp.Server.Models.Identity.RefreshToken
        {
            Token = "brand-new-refresh", UserId = userId,
            Expires = DateTimeOffset.UtcNow.AddDays(14)
        };
        var refreshService = Substitute.For<IRefreshTokenService>();
        refreshService.IssueTokenAsync(user, Arg.Any<TimeSpan>()).Returns(fakeRefresh);

        var jwtService = Substitute.For<IJwtTokenService>();
        jwtService.GenerateAccessTokenAsync(user, Arg.Any<bool>())
                  .Returns(("brand.new.jwt", DateTime.UtcNow.AddHours(1)));

        var controller = BuildController(
            userManager: userManager,
            refreshTokenService: refreshService,
            jwtTokenService: jwtService,
            principal: BuildPrincipal(userId));

        var result = await controller.ChangePassword(new ChangePassword
        {
            CurrentPassword = "Old!1", Password = "New!2"
        });

        Assert.IsType<OkObjectResult>(result.Result);
        // New AuthToken cookie must be set
        var authCookie = controller.HttpContext.Response.Headers["Set-Cookie"]
            .FirstOrDefault(h => h is not null && h.StartsWith("AuthToken="));
        Assert.NotNull(authCookie);
        Assert.Contains("brand.new.jwt", authCookie);
    }

    [Fact]
    public async Task ResetPassword_Success_RevokesAllUserTokens()
    {
        const string userId = "user-88";
        var user        = BuildUser(id: userId, userName: "resetuser");
        var userManager = BuildUserManager();
        userManager.FindByEmailAsync(user.Email!).Returns(user);
        userManager.ResetPasswordAsync(user, Arg.Any<string>(), Arg.Any<string>())
                   .Returns(IdentityResult.Success);

        var refreshService = Substitute.For<IRefreshTokenService>();

        var controller = BuildController(
            userManager: userManager,
            refreshTokenService: refreshService);

        await controller.ResetPassword(new ResetPasswordRequest
        {
            Email    = user.Email!,
            Token    = "valid-reset-token",
            Password = "New!3"
        });

        await refreshService.Received(1).RevokeAllUserTokensAsync(userId);
    }

    // ── Unknown user and wrong password return identical response shape ────────

    [Fact]
    public async Task Login_UnknownUser_AndWrongPassword_ReturnIdenticalStatusAndDtoShape()
    {
        // Unknown user
        var umNotFound = BuildUserManager();
        umNotFound.FindByNameAsync(Arg.Any<string>()).Returns((ApplicationUser?)null);
        umNotFound.FindByEmailAsync(Arg.Any<string>()).Returns((ApplicationUser?)null);
        var notFoundResult = await BuildController(userManager: umNotFound).Login(
            new LoginRequest { Username = "ghost", Password = "any", RememberMe = false });

        // Wrong password
        var umBadPass = BuildUserManager();
        var user      = BuildUser();
        umBadPass.FindByNameAsync(user.UserName!).Returns(user);
        var sm = BuildSignInManager(umBadPass);
        sm.PasswordSignInAsync(user, Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>())
          .Returns(Microsoft.AspNetCore.Identity.SignInResult.Failed);
        var badPassResult = await BuildController(userManager: umBadPass, signInManager: sm).Login(
            new LoginRequest { Username = user.UserName!, Password = "wrong", RememberMe = false });

        var okNotFound = Assert.IsType<OkObjectResult>(notFoundResult.Result);
        var okBadPass  = Assert.IsType<OkObjectResult>(badPassResult.Result);
        var dtoNotFound = Assert.IsType<SignInResultDto>(okNotFound.Value);
        var dtoBadPass  = Assert.IsType<SignInResultDto>(okBadPass.Value);

        Assert.Equal(okNotFound.StatusCode, okBadPass.StatusCode);
        Assert.Equal(dtoNotFound.Succeeded, dtoBadPass.Succeeded);
        Assert.Equal(dtoNotFound.Message, dtoBadPass.Message);
    }
}
