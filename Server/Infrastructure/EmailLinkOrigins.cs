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
    public bool IsAllowed(string? url) =>
        _allowed.Count == 0 || (Uri.TryCreate(url, UriKind.Absolute, out var uri) && _allowed.Contains(OriginOf(uri)));

    /// <summary>
    /// The link to email: <paramref name="clientUrl"/>'s origin (which must pass the allow-list)
    /// plus a server-owned <paramref name="path"/> — the client's own path, query and fragment are
    /// dropped, so it can only choose which of our sites the link opens, never which page. Null
    /// when the URL isn't an absolute http(s) URL on an allowed origin.
    /// </summary>
    public string? LinkTo(string? clientUrl, string path)
    {
        if (!Uri.TryCreate(clientUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            return null;
        var origin = OriginOf(uri);
        return _allowed.Count == 0 || _allowed.Contains(origin) ? origin + path : null;
    }

    // Uri.Authority excludes userinfo, so "https://ivleague.xyz@evil.example" resolves to
    // evil.example and is rejected.
    private static string OriginOf(Uri uri) => $"{uri.Scheme}://{uri.Authority}";
}

/// <summary>Server-owned paths for emailed links — must match the routes in Client.React/src/App.tsx.</summary>
public static class AccountLinkPaths
{
    public const string ConfirmEmail = "/account/confirmemail";
    public const string ResetPassword = "/account/resetpassword";
    public const string Register = "/account/register";
}

/// <summary>Thrown when a service is asked to email a link to an origin <see cref="EmailLinkOrigins"/> rejects.</summary>
public sealed class UntrustedEmailLinkException()
    : ArgumentException("The link must point to one of this site's own origins.");
