using System.Security.Claims;
using FourPlayWebApp.Server.Auth;

namespace FourPlayWebApp.Server.UnitTests;

internal static class TestPrincipalFactory
{
    public static ClaimsPrincipal Build(string userId, string? email = null, bool isAdmin = false) =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId),
            .. (email is not null
                ? (Claim[])[new Claim(ClaimTypes.Email, email), new Claim(ClaimTypes.Name, email)]
                : []),
            .. (isAdmin ? (Claim[])[new Claim(ClaimTypes.Role, AppRoles.Administrator)] : []),
        ], "Test"));
}
