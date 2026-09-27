namespace FourPlayWebApp.Server.Infrastructure;

/// <summary>
/// Every emailed link (confirmation, password reset, league invite) is built from a
/// client-supplied URL — the server has no base URL config of its own. This is the one check that
/// URL is ours: an absolute URL whose origin is in ALLOWED_ORIGINS (the same comma-separated
/// allow-list used for CORS). Without it, anyone could make IV League send a real, branded email
/// linking to their own domain — for password reset, carrying a live reset code.
/// </summary>
internal static class EmailLinkOrigin
{
    /// <remarks>
    /// ALLOWED_ORIGINS is only ever empty in Development — Program.cs fails startup otherwise — so
    /// an empty list allows any origin, mirroring the AllowAnyOrigin() CORS fallback for that case.
    /// </remarks>
    internal static bool IsAllowed(IConfiguration config, string? url)
    {
        var allowedOrigins = (config["ALLOWED_ORIGINS"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (allowedOrigins.Length == 0)
            return true;

        // Uri.Authority excludes userinfo, so "https://ivleague.xyz@evil.example" resolves to
        // evil.example and is rejected.
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        var origin = $"{uri.Scheme}://{uri.Authority}";
        return allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase);
    }
}
