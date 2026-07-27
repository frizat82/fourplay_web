namespace FourPlayWebApp.Server.Infrastructure;

internal static class StartupValidation
{
    /// <summary>
    /// Parses ALLOWED_ORIGINS env var. Throws in non-Development when empty so a
    /// misconfigured deploy cannot silently open wildcard CORS.
    /// </summary>
    internal static string[] ParseAndValidateCorsOrigins(string? originsEnv, bool isDevelopment)
    {
        var origins = (originsEnv ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (origins.Length == 0 && !isDevelopment)
            throw new InvalidOperationException(
                "ALLOWED_ORIGINS must be set in non-Development environments. " +
                "Configure a comma-separated list of allowed origins (e.g. https://ivleague.com,https://cfb.ivleague.com).");

        return origins;
    }
}
