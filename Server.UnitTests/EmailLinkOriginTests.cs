using FourPlayWebApp.Server.Infrastructure;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

// Every server-sent email that carries a link (confirmation, password reset, league invite) is
// built from a client-supplied URL — the server has no base URL of its own. Without this check,
// anyone could make IV League send a real, branded email pointing at their own domain, and for
// password reset that link would carry a live reset code.
public class EmailLinkOriginTests
{
    private static IConfiguration Config(string? origins)
    {
        var config = Substitute.For<IConfiguration>();
        config["ALLOWED_ORIGINS"].Returns(origins);
        return config;
    }

    private static readonly IConfiguration Prod = Config("https://ivleague.xyz, https://cfb.ivleague.xyz");

    [Theory]
    [InlineData("https://ivleague.xyz/account/resetpassword")]
    [InlineData("https://cfb.ivleague.xyz/account/confirmemail")]
    [InlineData("https://IVLEAGUE.xyz")]
    [InlineData("https://ivleague.xyz/")]
    public void IsAllowed_TrustedOrigin_ReturnsTrue(string url) =>
        Assert.True(EmailLinkOrigin.IsAllowed(Prod, url));

    [Theory]
    [InlineData("https://evil.example/account/resetpassword")]
    [InlineData("https://ivleague.xyz.evil.example/reset")]   // lookalike suffix
    [InlineData("https://ivleague.xyz@evil.example/reset")]   // userinfo trick — real host is evil.example
    [InlineData("http://ivleague.xyz/reset")]                 // scheme downgrade
    [InlineData("https://ivleague.xyz:8443/reset")]           // different port = different origin
    [InlineData("/account/resetpassword")]                    // relative
    [InlineData("javascript:alert(1)")]
    [InlineData("")]
    [InlineData(null)]
    public void IsAllowed_UntrustedOrMalformed_ReturnsFalse(string? url) =>
        Assert.False(EmailLinkOrigin.IsAllowed(Prod, url));

    // ALLOWED_ORIGINS is only ever empty in Development (Program.cs fails startup otherwise),
    // matching the AllowAnyOrigin() CORS fallback for that case.
    [Fact]
    public void IsAllowed_Unconfigured_AllowsAnything() =>
        Assert.True(EmailLinkOrigin.IsAllowed(Config(null), "https://anything.example/x"));
}
