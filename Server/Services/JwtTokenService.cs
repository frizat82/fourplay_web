using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace FourPlayWebApp.Server.Services;

public class JwtTokenService(IConfiguration config, UserManager<ApplicationUser> userManager, ILogger<JwtTokenService> logger) : IJwtTokenService
{
    // We'll build TokenValidationParameters on demand to read latest config values.
    private readonly IConfiguration _config = config;
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly ILogger<JwtTokenService> _logger = logger;

    private TokenValidationParameters BuildValidationParameters()
    {
        var jwtSection = _config.GetSection("Jwt");
        var key = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured");
        var keyBytes = Encoding.UTF8.GetBytes(key);

        return new TokenValidationParameters
        {
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    }

    public async Task<(string Token, DateTime Expires)> GenerateAccessTokenAsync(ApplicationUser user, bool rememberMe = false)
    {
        try
        {
            var jwtSection = _config.GetSection("Jwt");
            var key = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured");
            var keyBytes = Encoding.UTF8.GetBytes(key);
            var signingKey = new SymmetricSecurityKey(keyBytes);
            var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id),
                new(ClaimTypes.Name, user.UserName ?? user.Email ?? user.Id)
            };

            var roles = await _userManager.GetRolesAsync(user);
            claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
            var userClaims = await _userManager.GetClaimsAsync(user);
            claims.AddRange(userClaims);

            var expiresMinutes = int.Parse(jwtSection["ExpiresMinutes"] ?? "60");
            var jwtExpires = DateTime.UtcNow.AddMinutes(expiresMinutes);

            var token = new JwtSecurityToken(
                issuer: jwtSection["Issuer"],
                audience: jwtSection["Audience"],
                claims: claims,
                expires: jwtExpires,
                signingCredentials: creds);

            var jwt = new JwtSecurityTokenHandler().WriteToken(token);
            return (jwt, jwtExpires);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate JWT for user {UserId}", user?.Id);
            throw;
        }
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            var validationParameters = BuildValidationParameters();
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, validationParameters, out var validatedToken);
            return principal;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid JWT token");
            return null;
        }
    }

    public TokenValidationParameters GetValidationParameters() => BuildValidationParameters();
}
