using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// Unit tests for JwtTokenService.GenerateAccessTokenAsync.
/// Verifies that the service correctly reads JWT configuration from IConfiguration,
/// embeds the right claims, and produces a well-formed signed JWT.
/// </summary>
public class JwtTokenServiceTests
{
    // A key long enough for HMAC-SHA256 (>=32 bytes / 256 bits)
    private const string TestKey = "SuperSecretTestKey_ForUnitTests_AtLeast32Bytes!";
    private const string TestIssuer = "test-issuer";
    private const string TestAudience = "test-audience";
    private const int TestExpiresMinutes = 60;

    private static (JwtTokenService Service, UserManager<ApplicationUser> UserManager)
        BuildService(int? expiresMinutes = null)
    {
        var inMemoryConfig = new Dictionary<string, string?>
        {
            ["Jwt:Key"]            = TestKey,
            ["Jwt:Issuer"]         = TestIssuer,
            ["Jwt:Audience"]       = TestAudience,
            ["Jwt:ExpiresMinutes"] = (expiresMinutes ?? TestExpiresMinutes).ToString(),
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemoryConfig)
            .Build();

        var userStore   = Substitute.For<IUserStore<ApplicationUser>>();
        var userManager = UserManagerStub.Create(userStore);

        // Default: no roles, no extra claims
        userManager.GetRolesAsync(Arg.Any<ApplicationUser>())
                   .Returns(new List<string>());
        userManager.GetClaimsAsync(Arg.Any<ApplicationUser>())
                   .Returns(new List<Claim>());

        var service = new JwtTokenService(config, userManager, NullLogger<JwtTokenService>.Instance);
        return (service, userManager);
    }

    private static ApplicationUser BuildUser(string id = "user-123", string userName = "testuser") =>
        new() { Id = id, UserName = userName, Email = $"{userName}@example.com" };

    // ── Helper: decode payload without verification ──────────────────────────

    private static JsonDocument DecodePayload(string jwt)
    {
        var parts = jwt.Split('.');
        // Base64Url → standard base64
        var payload = parts[1];
        var padded  = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=')
                             .Replace('-', '+').Replace('_', '/');
        var bytes = Convert.FromBase64String(padded);
        return JsonDocument.Parse(Encoding.UTF8.GetString(bytes));
    }

    // ── Test 1: Token is a valid JWT string ──────────────────────────────────

    [Fact]
    public async Task GenerateAccessToken_ReturnsNonEmptyJwtString()
    {
        var (svc, _) = BuildService();
        var user = BuildUser();

        var (token, expires) = await svc.GenerateAccessTokenAsync(user);

        Assert.NotNull(token);
        Assert.NotEmpty(token);
        // JWT = header.payload.signature → two dots
        Assert.Equal(2, token.Count(c => c == '.'));
        Assert.True(expires > DateTime.UtcNow);
    }

    // ── Test 2: Token contains correct issuer claim ──────────────────────────

    [Fact]
    public async Task GenerateAccessToken_ContainsCorrectIssuer()
    {
        var (svc, _) = BuildService();
        var user = BuildUser();

        var (token, _) = await svc.GenerateAccessTokenAsync(user);

        var payload = DecodePayload(token);
        Assert.True(payload.RootElement.TryGetProperty("iss", out var iss));
        Assert.Equal(TestIssuer, iss.GetString());
    }

    // ── Test 3: Token contains correct audience claim ────────────────────────

    [Fact]
    public async Task GenerateAccessToken_ContainsCorrectAudience()
    {
        var (svc, _) = BuildService();
        var user = BuildUser();

        var (token, _) = await svc.GenerateAccessTokenAsync(user);

        var payload = DecodePayload(token);

        // "aud" may be a string or a JSON array
        Assert.True(payload.RootElement.TryGetProperty("aud", out var aud));
        var audValue = aud.ValueKind == JsonValueKind.Array
            ? aud.EnumerateArray().Select(e => e.GetString()).ToList()
            : new List<string?> { aud.GetString() };

        Assert.Contains(TestAudience, audValue);
    }

    // ── Test 4: Token contains userId claim (NameIdentifier) ─────────────────

    [Fact]
    public async Task GenerateAccessToken_ContainsNameIdentifierClaim_MatchingUserId()
    {
        var (svc, _) = BuildService();
        var user = BuildUser(id: "abc-999");

        var (token, _) = await svc.GenerateAccessTokenAsync(user);

        var handler    = new JwtSecurityTokenHandler();
        var parsed     = handler.ReadJwtToken(token);
        var nameIdClaim = parsed.Claims
            .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier
                              || c.Type == JwtRegisteredClaimNames.NameId
                              || c.Type == "nameid");

        Assert.NotNull(nameIdClaim);
        Assert.Equal(user.Id, nameIdClaim.Value);
    }

    // ── Test 5: Token contains name claim matching UserName ──────────────────

    [Fact]
    public async Task GenerateAccessToken_ContainsNameClaim_MatchingUserName()
    {
        var (svc, _) = BuildService();
        var user = BuildUser(userName: "johndoe");

        var (token, _) = await svc.GenerateAccessTokenAsync(user);

        var handler  = new JwtSecurityTokenHandler();
        var parsed   = handler.ReadJwtToken(token);
        var nameClaim = parsed.Claims
            .FirstOrDefault(c => c.Type == ClaimTypes.Name
                              || c.Type == JwtRegisteredClaimNames.UniqueName
                              || c.Type == "unique_name");

        Assert.NotNull(nameClaim);
        Assert.Equal(user.UserName, nameClaim.Value);
    }

    // ── Test 6: Token contains role claim when user has a role ───────────────

    [Fact]
    public async Task GenerateAccessToken_ContainsRoleClaim_WhenUserHasRole()
    {
        var (svc, userManager) = BuildService();
        var user = BuildUser();

        userManager.GetRolesAsync(user)
                   .Returns(new List<string> { "Administrator" });

        var (token, _) = await svc.GenerateAccessTokenAsync(user);

        var handler  = new JwtSecurityTokenHandler();
        var parsed   = handler.ReadJwtToken(token);
        var roleClaim = parsed.Claims
            .FirstOrDefault(c => c.Type == ClaimTypes.Role
                              || c.Type == "role");

        Assert.NotNull(roleClaim);
        Assert.Equal("Administrator", roleClaim.Value);
    }

    // ── Test 7: Token expiry uses ExpiresMinutes from config ─────────────────
    // Note: JwtTokenService ignores the rememberMe parameter for expiry;
    // expiry is always driven by Jwt:ExpiresMinutes in IConfiguration.

    [Fact]
    public async Task GenerateAccessToken_ExpiryMatchesConfiguredMinutes()
    {
        const int configuredMinutes = 60;
        var (svc, _) = BuildService(expiresMinutes: configuredMinutes);
        var user = BuildUser();
        var before = DateTime.UtcNow;

        var (_, expires) = await svc.GenerateAccessTokenAsync(user);

        var after = DateTime.UtcNow;
        var expectedMin = before.AddMinutes(configuredMinutes - 1);
        var expectedMax = after.AddMinutes(configuredMinutes + 1);

        Assert.InRange(expires, expectedMin, expectedMax);
    }

    // ── Test 8: RememberMe flag does NOT alter expiry (current implementation) ─

    [Fact]
    public async Task GenerateAccessToken_RememberMeTrue_ExpiryStillUsesConfigMinutes()
    {
        // The current JwtTokenService implementation ignores rememberMe when
        // calculating expiry — it always reads Jwt:ExpiresMinutes from config.
        const int configuredMinutes = 60;
        var (svc, _) = BuildService(expiresMinutes: configuredMinutes);
        var user = BuildUser();
        var before = DateTime.UtcNow;

        var (_, expires) = await svc.GenerateAccessTokenAsync(user, rememberMe: true);

        var after = DateTime.UtcNow;
        var expectedMin = before.AddMinutes(configuredMinutes - 1);
        var expectedMax = after.AddMinutes(configuredMinutes + 1);

        Assert.InRange(expires, expectedMin, expectedMax);
    }

    // ── Test 9: Token signature validates correctly ───────────────────────────

    [Fact]
    public async Task GenerateAccessToken_TokenValidatesWithSameKey()
    {
        var (svc, _) = BuildService();
        var user = BuildUser();

        var (token, _) = await svc.GenerateAccessTokenAsync(user);

        // ValidateToken should return a non-null principal for a valid token
        var principal = svc.ValidateToken(token);
        Assert.NotNull(principal);
    }

    // frizat-501: JwtBearer 8+ validates with JsonWebTokenHandler, not the JwtSecurityTokenHandler
    // that writes our tokens (which shortens claim names: nameid / unique_name / role). The app
    // reads ClaimTypes.NameIdentifier everywhere and [Authorize(Roles = "Administrator")] gates
    // admin endpoints, so a token we issue must come back with the standard claim types through
    // the handler the middleware actually uses, configured the way Program.cs leaves it.
    [Fact]
    public async Task IssuedToken_ReadByTheJwtBearerMiddlewaresHandler_KeepsUserIdNameAndRole()
    {
        var (svc, userManager) = BuildService();
        var user = BuildUser("user-42", "alice");
        userManager.GetRolesAsync(user).Returns(new List<string> { "Administrator" });
        var (token, _) = await svc.GenerateAccessTokenAsync(user);

        var options = new JwtBearerOptions(); // MapInboundClaims and TokenHandlers at their defaults, as in Program.cs
        var handler = Assert.Single(options.TokenHandlers);
        var result = await handler.ValidateTokenAsync(token, new TokenValidationParameters {
            ValidIssuer = TestIssuer, ValidAudience = TestAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestKey)),
        });

        Assert.True(result.IsValid, result.Exception?.Message);
        var principal = new ClaimsPrincipal(result.ClaimsIdentity);
        Assert.Equal("user-42", principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("alice", principal.FindFirstValue(ClaimTypes.Name));
        Assert.True(principal.IsInRole("Administrator"));
    }
}
