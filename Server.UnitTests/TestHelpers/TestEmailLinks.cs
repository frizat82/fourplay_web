using FourPlayWebApp.Server.Infrastructure;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>Shared <see cref="EmailLinkOrigins"/> instances, so the origin list isn't re-typed per test.</summary>
internal static class TestEmailLinks {
    public static readonly string[] ProdOrigins = ["https://ivleague.xyz", "https://cfb.ivleague.xyz"];

    /// <summary>Production-shaped allow-list: NFL and CFB sites.</summary>
    public static EmailLinkOrigins Prod => new(ProdOrigins);

    /// <summary>Empty ALLOWED_ORIGINS — the Development case, where every origin is allowed.</summary>
    public static EmailLinkOrigins AllowAny => new([]);
}
