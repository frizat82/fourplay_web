using Npgsql;

namespace FourPlayWebApp.Server.Infrastructure;

internal static class PostgresConnectionString
{
    /// <summary>
    /// The Npgsql connection string for the app and EF design-time, from either a postgres:// URL
    /// (what Neon and most managed providers hand out) or a key=value string (local Docker
    /// Postgres) — written out by Npgsql's builder, so keys come back in its canonical form.
    /// GSS (Kerberos) encryption is turned off unless the string asks for a specific mode: Neon and
    /// local Postgres never use it, and at Npgsql's default (Prefer) the first connection tries to
    /// load libgssapi_krb5, which the .NET 10 runtime image doesn't ship — an error line on every boot.
    /// </summary>
    internal static string Normalize(string raw)
    {
        var builder = raw.StartsWith("postgres://") || raw.StartsWith("postgresql://")
            ? FromUrl(new Uri(raw))
            : new NpgsqlConnectionStringBuilder(raw);
        if (builder.GssEncryptionMode == GssEncryptionMode.Prefer)
            builder.GssEncryptionMode = GssEncryptionMode.Disable;
        return builder.ConnectionString;
    }

    private static NpgsqlConnectionStringBuilder FromUrl(Uri uri)
    {
        var userInfo = uri.UserInfo.Split(':', 2);
        var sslMode = System.Web.HttpUtility.ParseQueryString(uri.Query)["sslmode"]?.ToLowerInvariant() switch {
            "require"     => SslMode.Require,
            "verify-ca"   => SslMode.VerifyCA,
            "verify-full" => SslMode.VerifyFull,
            "disable"     => SslMode.Disable,
            _             => SslMode.Prefer,
        };
        return new NpgsqlConnectionStringBuilder {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty,
            Database = uri.AbsolutePath.TrimStart('/'),
            // Require/Prefer encrypt without validating the certificate; VerifyCA/VerifyFull validate
            // the chain (Npgsql 6+ — so no "Trust Server Certificate", which is now a no-op).
            SslMode = sslMode,
        };
    }
}
