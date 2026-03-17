using FourPlayWebApp.Server.Models.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace FourPlayWebApp.Server.Services.Interfaces;

public interface IJwtTokenService
{
    /// <summary>
    /// Generates a JWT access token for the specified user and returns the token and expiration time.
    /// </summary>
    Task<(string Token, DateTime Expires)> GenerateAccessTokenAsync(ApplicationUser user, bool rememberMe = false);

    /// <summary>
    /// Validates a JWT and returns the principal if valid, otherwise null.
    /// </summary>
    ClaimsPrincipal? ValidateToken(string token);

    /// <summary>
    /// Returns the TokenValidationParameters used by the service.
    /// </summary>
    TokenValidationParameters GetValidationParameters();
}

