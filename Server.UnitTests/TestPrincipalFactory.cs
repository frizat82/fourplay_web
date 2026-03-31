using System.Security.Claims;

namespace FourPlayWebApp.Server.UnitTests;

internal static class TestPrincipalFactory
{
    public static ClaimsPrincipal Build(string userId, string? email = null) =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId),
            .. (email is not null
                ? (Claim[])[new Claim(ClaimTypes.Email, email), new Claim(ClaimTypes.Name, email)]
                : []),
        ], "Test"));
}
