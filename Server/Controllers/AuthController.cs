using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models.Account;
using FourPlayWebApp.Shared.Models.Account.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;

namespace FourPlayWebApp.Server.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IEmailSender emailSender, IEmailSender<ApplicationUser> emailSenderApplication,
    IInvitationService invitationService, ILogger<AuthController> logger,
    IConfiguration config, IRefreshTokenService refreshTokenService, IJwtTokenService jwtTokenService,
    IWebHostEnvironment environment, ApplicationDbContext db)
    : ControllerBase {
    private readonly TimeSpan _refreshTokenLifetime = TimeSpan.FromDays(14); // 14 days
    private bool UseSecureCookies => !environment.IsDevelopment() || Request.IsHttps;

    private CookieOptions BuildCookieOptions(DateTimeOffset? expires = null) => new() {
        HttpOnly = true,
        Secure = UseSecureCookies,
        SameSite = UseSecureCookies ? SameSiteMode.None : SameSiteMode.Lax,
        Expires = expires
    };

    private void ExpireAuthCookies() {
        var expiredAt = DateTimeOffset.UtcNow.AddDays(-1);
        Response.Cookies.Append("AuthToken", "", BuildCookieOptions(expiredAt));
        Response.Cookies.Append("RefreshToken", "", BuildCookieOptions(expiredAt));
    }

    // POST /api/auth/login
    [HttpPost("login")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth")]
    public async Task<ActionResult<SignInResultDto>> Login([FromBody] LoginRequest req)
    {
        try {
            var user = await userManager.FindByNameAsync(req.Username)
                       ?? await userManager.FindByEmailAsync(req.Username);

            if (user is null)
                return Unauthorized();

            var result = await signInManager.PasswordSignInAsync(
                user,
                req.Password,
                isPersistent: req.RememberMe,
                lockoutOnFailure: true
            );
            var dto = new SignInResultDto {
                Succeeded = result.Succeeded,
                IsLockedOut = result.IsLockedOut,
                RequiresTwoFactor = result.RequiresTwoFactor,
                IsNotAllowed = result.IsNotAllowed,
                AccessFailedCount = await userManager.GetAccessFailedCountAsync(user),
                LockoutEnd = user.LockoutEnd,
            };
            if (!result.Succeeded) {
                dto.Message = "Invalid credentials";
            }

            if (result.IsLockedOut) {
                dto.Message = "User is locked out";
            }
            else if (result.RequiresTwoFactor) {
                dto.Message = "Two-factor authentication required";
            }
            else if (result.IsNotAllowed) {
                dto.Message = "User is not allowed to sign in";
            }

            if (result.Succeeded)
            {
                // Use JWT service to create token
                var (jwt, jwtExpires) = await jwtTokenService.GenerateAccessTokenAsync(user, req.RememberMe);

                // Set both cookies
                Response.Cookies.Append("AuthToken", jwt, BuildCookieOptions(jwtExpires));

                if (req.RememberMe) {
                    // Issue refresh token
                    var refreshToken = await refreshTokenService.IssueTokenAsync(user, _refreshTokenLifetime);

                    Response.Cookies.Append("RefreshToken", refreshToken.Token, BuildCookieOptions(refreshToken.Expires));
                }

                dto.Message = "Login successful";
            }

            return Ok(dto);
        } catch (Exception e) {
            logger.LogError(e, "Error during login");
            return Unauthorized();
        }
    }

    [HttpPost("refresh")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("refresh")]
    public async Task<IActionResult> Refresh()
    {
        // Get the refresh token from cookie
        if (!Request.Cookies.TryGetValue("RefreshToken", out var refreshTokenValue))
        {
            return Unauthorized();
        }

        // Validate the refresh token
        var refreshToken = await refreshTokenService.ValidateTokenAsync(refreshTokenValue);
        if (refreshToken == null)
        {
            // Clear cookies
            ExpireAuthCookies();
            return Unauthorized();
        }

        var user = refreshToken.User;
        
        // Rotate refresh token
        var newRefreshToken = await refreshTokenService.RotateTokenAsync(refreshTokenValue, user, _refreshTokenLifetime);
        if (newRefreshToken == null)
        {
            return Unauthorized();
        }

        // Issue new JWT using jwt service
        var (jwt, jwtExpires) = await jwtTokenService.GenerateAccessTokenAsync(user, rememberMe: true);

        // Set new cookies
        Response.Cookies.Append("AuthToken", jwt, BuildCookieOptions(jwtExpires));
        Response.Cookies.Append("RefreshToken", newRefreshToken.Token, BuildCookieOptions(newRefreshToken.Expires));

        return Ok();
    }

    // POST /api/auth/logout
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        // Revoke refresh token if present
        if (Request.Cookies.TryGetValue("RefreshToken", out var refreshToken))
        {
            await refreshTokenService.RevokeTokenAsync(refreshToken);
        }

        ExpireAuthCookies();

        return Ok(new { ok = true });
    }
    // GET /api/auth/me
    [Authorize]
    [HttpGet("me")]
    public ActionResult<UserInfo> Me()
    {
        var identity = User.Identity;
        if (identity?.IsAuthenticated != true)
            return Unauthorized();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var name   = User.Identity?.Name;

        return Ok(new UserInfo(userId!, name!,
            User.Claims.Select(c => new ClaimDto(c.Type, c.Value)).ToList()
        ));
    }
    [HttpPost("confirm-email")]
    public async Task<ActionResult<string>> ConfirmEmail([FromBody] ConfirmEmailRequest request)
    {
        var user = await userManager.FindByIdAsync(request.UserId);
        if (user == null)
            return NotFound();
        var result = await userManager.ConfirmEmailAsync(user, request.Token);
        if (result.Errors.Any()) {
            logger.LogError("Error confirming email for user {UserId}: {@Errors}", request.UserId, result.Errors);
            return BadRequest(string.Join(Environment.NewLine, result.Errors.Select(x => x.Description)));
        }

        logger.LogInformation("User {UserId} confirmed email", request.UserId);
        return Ok();
    }
    [Authorize(Roles = "Administrator")]
    [HttpPost("does-user-exist/{email}")]
    public async Task<ActionResult<bool>> DoesUserExist(string email)
    {
        var user = await userManager.FindByEmailAsync(email);
        return Ok(user != null);
    }
    [Authorize(Roles = "Administrator")]
    [HttpPost("assign-user-role")]
    public async Task<ActionResult<string>> AssignUserRole(
        [FromBody] AssignRoleRequest request) // default value
    {
        var user = await userManager.FindByIdAsync(request.UserId);
        if (user == null)
            return NotFound();

        var result = await userManager.AddToRoleAsync(user, request.Role); // assign role

        if (!result.Succeeded)
            return BadRequest(string.Join(Environment.NewLine, result.Errors.Select(e => e.Description)));

        return Ok();
    }

    [Authorize(Roles = "Administrator")]
    [HttpPost("delete-user/{userId}")]
    public async Task<ActionResult<string>> DeleteUser(string userId) {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
            return NotFound();
        var result = await userManager.DeleteAsync(user);
        if (result.Errors.Any())
            return BadRequest(string.Join(Environment.NewLine, result.Errors.Select(x => x.Description)));
        return Ok();
    }
    [Authorize(Roles = "Administrator")]
    [HttpGet("admin-confirm-email/{userId}")]
    public async Task<ActionResult<string>> ConfirmEmail(string userId)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
            return NotFound();
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var result = await userManager.ConfirmEmailAsync(user, token);
        if (result.Errors.Any())
            return BadRequest(string.Join(Environment.NewLine, result.Errors.Select(x => x.Description)));
        return Ok();
    }
    [AllowAnonymous]
    [HttpPost("create-user")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("register")]
    public async Task<ActionResult<string>> CreateUser([FromBody] CreateUserRequest user) {
        // Validate invitation code
        var response = new CreateUserResponse();
        var invitation = await invitationService.ValidateInvitationAsync(user.Code);
        if (invitation == null) {
            response.IsSuccess = false;
            response.Errors = new List<string> { "Invalid or expired invitation code." };
            return BadRequest(response);      
        } //
        // Check if the email matches the invitation
        if (!string.Equals(user.Email, invitation.Email, StringComparison.OrdinalIgnoreCase))
        {
            response.IsSuccess = false;
            response.Errors = new List<string> { "Email does not match invitation." };
            return BadRequest(response);       
        }
        var doesUserExist = await userManager.FindByEmailAsync(user.Email);
        if (doesUserExist != null) {
            response.IsSuccess = false;
            response.Errors.Add("User already exists.");
            return BadRequest(response);
        }

        var newUser = new ApplicationUser { UserName = user.Username, Email = user.Email };
        var result = await userManager.CreateAsync(newUser, user.Password);
        if (result.Errors.Any()) {
            response.IsSuccess = false;
            response.Errors = result.Errors.Select(x => x.Description).ToList();
            return BadRequest(response);
        }

        response.IsSuccess = true;
        response.UserId = newUser.Id;
        // Mark invitation as used
        var invitationResult = await invitationService.MarkInvitationAsUsedAsync(user.Code, newUser.Id);
        if (!invitationResult) return BadRequest("Invalid invitation code or already used/expired.");

        // Auto-assign user to league if invitation specifies one
        if (invitation.LeagueId.HasValue)
        {
            db.LeagueUserMapping.Add(new LeagueUserMapping
            {
                LeagueId = invitation.LeagueId.Value,
                UserId = newUser.Id,
            });
            await db.SaveChangesAsync();
        }

        // Send confirmation email server-side — do not rely on client making a second call.
        // Failure is logged but must not prevent the user account from being returned as created.
        try {
            var token = await userManager.GenerateEmailConfirmationTokenAsync(newUser);
            var code = System.Text.Encoding.UTF8.GetBytes(token);
            var encodedCode = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(code);
            var confirmationUrl = $"{config["App:BaseUrl"]?.TrimEnd('/') ?? string.Empty}/account/confirmemail";
            var callbackUrl = $"{confirmationUrl}?userId={newUser.Id}&code={encodedCode}";
            await emailSenderApplication.SendConfirmationLinkAsync(newUser, newUser.Email!, System.Text.Encodings.Web.HtmlEncoder.Default.Encode(callbackUrl));
        }
        catch (Exception ex) {
            logger.LogError(ex, "Failed to send confirmation email to {Email} after registration; user was still created", newUser.Email);
        }

        return Ok(response);
    }
    [HttpPost("forgot-password")]
    [AllowAnonymous] // User is not logged in
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("forgot")]
    public async Task<ActionResult<string>> ForgotPassword([FromBody] ForgotPasswordRequest model)
    {
        if (string.IsNullOrWhiteSpace(model.Email) ||
            string.IsNullOrWhiteSpace(model.ResetUrl))
        {
            return BadRequest("Invalid request.");
        }

        var user = await userManager.FindByEmailAsync(model.Email);
        if (user == null)
            return Ok(); // Don't reveal whether the email exists

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var callbackUrl = $"{model.ResetUrl}?code={code}";

        await emailSenderApplication.SendPasswordResetLinkAsync(user, model.Email, HtmlEncoder.Default.Encode(callbackUrl));
        return Ok();
    }    
    [HttpPost("reset-password")]
    [AllowAnonymous] // User is not logged in
    public async Task<ActionResult<string>> ResetPassword([FromBody] ResetPasswordRequest model)
    {
        if (string.IsNullOrWhiteSpace(model.Email) ||
            string.IsNullOrWhiteSpace(model.Token) ||
            string.IsNullOrWhiteSpace(model.Password))
        {
            return BadRequest("Invalid request.");
        }

        var user = await userManager.FindByEmailAsync(model.Email);
        if (user == null)
            return Ok(); // Don't reveal whether the email exists

        var result = await userManager.ResetPasswordAsync(user, model.Token, model.Password);
        if (!result.Succeeded)
            return BadRequest("Invalid request.");
        return Ok();
    }
    [HttpPost("change-password")]
    [Authorize]
    public async Task<ActionResult<string>> ChangePassword([FromBody] ChangePassword model)
    {
        if (string.IsNullOrWhiteSpace(model.Password))
            return BadRequest("Invalid request.");

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await userManager.FindByIdAsync(userId!);
        if (user == null)
            return BadRequest("Invalid request.");

        var result = await userManager.ChangePasswordAsync(user, model.CurrentPassword, model.Password);
        if (!result.Succeeded)
            return BadRequest("Invalid request.");
        return Ok();
    }
    [HttpPost("request-email-confirmation")]
    [AllowAnonymous]
    public async Task<ActionResult<string>> RequestEmailConfirmation([FromBody] RequestEmailConfirmation request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        // Always respond the same way
        if (user == null || await userManager.IsEmailConfirmedAsync(user))
            return Ok("If your email is registered, you will receive a confirmation link.");
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var callbackUrl = $"{request.ConfirmationUrl}?userId={user.Id}&code={code}";
        await emailSenderApplication.SendConfirmationLinkAsync(user, request.Email, HtmlEncoder.Default.Encode(callbackUrl));
        return Ok("If your email is registered, you will receive a confirmation link." );
    }


}
