namespace FourPlayWebApp.Server.Infrastructure;

/// <summary>
/// Every emailed link (confirmation, password reset, league invite) is built from a
/// client-supplied URL — the server has no base URL config of its own. This is the one check that
/// URL is ours: an absolute URL whose origin is in ALLOWED_ORIGINS. Registered as a singleton from
/// the same parsed array Program.cs gives CORS, so the two allow-lists can't drift. Without it,
/// anyone could make IV League send a real, branded email linking to their own domain — for
/// password reset, carrying a live reset code.
/// </summary>
public sealed class EmailLinkOrigins(IReadOnlyCollection<string> allowedOrigins)
{
    private readonly HashSet<string> _allowed = new(allowedOrigins, StringComparer.OrdinalIgnoreCase);

    /// <remarks>
    /// ALLOWED_ORIGINS is only ever empty in Development — Program.cs fails startup otherwise — so
    /// an empty list allows any origin, mirroring the AllowAnyOrigin() CORS fallback for that case.
    /// </remarks>
    public bool IsAllowed(string? url)
    {
        if (_allowed.Count == 0)
            return true;

        // Uri.Authority excludes userinfo, so "https://ivleague.xyz@evil.example" resolves to
        // evil.example and is rejected.
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && _allowed.Contains($"{uri.Scheme}://{uri.Authority}");
    }
}

/// <summary>Thrown when a service is asked to email a link to an origin <see cref="EmailLinkOrigins"/> rejects.</summary>
public sealed class UntrustedEmailLinkException()
    : ArgumentException("The link must point to one of this site's own origins.");
