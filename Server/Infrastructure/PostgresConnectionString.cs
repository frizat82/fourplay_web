namespace FourPlayWebApp.Server.Infrastructure;

internal static class PostgresConnectionString
{
    /// <summary>
    /// Neon (and most managed Postgres providers) hand out postgres://-style URLs; Npgsql wants
    /// Host=...;Port=...;... key=value form. Converts when needed, passes through unchanged
    /// otherwise (e.g. local Docker Postgres already uses key=value).
    /// </summary>
    internal static string Normalize(string raw)
    {
        if (!raw.StartsWith("postgres://") && !raw.StartsWith("postgresql://"))
            return raw;

        var uri = new Uri(raw);
        var userInfo = uri.UserInfo.Split(':');
        var username = userInfo[0];
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
        var host = uri.Host;
        var port = uri.Port > 0 ? uri.Port : 5432;
        var database = uri.AbsolutePath.TrimStart('/');
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var sslMode = query["sslmode"] ?? "Prefer";
        var npgsqlSsl = sslMode.ToLowerInvariant() switch {
            "require"     => "Require",
            "verify-ca"   => "VerifyCA",
            "verify-full" => "VerifyFull",
            "disable"     => "Disable",
            _             => "Prefer"
        };
        // Only trust-cert for Require/Prefer — VerifyCA/VerifyFull must validate the chain
        var trustCert = npgsqlSsl is "Require" or "Prefer" ? ";Trust Server Certificate=true" : string.Empty;
        return $"Host={host};Port={port};Username={username};Password={password};Database={database};SSL Mode={npgsqlSsl}{trustCert}";
    }
}
