using System.Text.Json;
using FourPlayWebApp.Shared.Models;
using Serilog;

namespace FourPlayWebApp.Server.Services;

/// <summary>
/// Shared "read a captured ESPN fixture" logic for the demo-mode cache services
/// (DemoEspnCacheService, DemoCfbCacheService) — kept as one implementation per CLAUDE.md's rule
/// against hand-synced NFL/CFB forks, which has repeatedly drifted into bugs.
///
/// Fixtures are embedded resources (see FourPlayWebApp.Server.csproj), not loose files read via
/// IWebHostEnvironment.ContentRootPath — a prior Path.Combine(ContentRootPath, "..", fileName)
/// version only worked by coincidence locally (running `dotnet run` from Server/ puts the repo
/// root exactly one level up); on Railway, ContentRootPath is the deployed app's own directory
/// (e.g. /app), so ".." resolved to the container filesystem root and silently found nothing —
/// DEMO_MODE's "live in-progress game" scenario (and, on NFL, the postseason weeks that only
/// exist in this fixture) quietly never worked once deployed.
/// </summary>
internal static class DemoFixtureLoader
{
    public static EspnScores? Load(string fileName, Func<string, string>? transformJson = null)
    {
        using var stream = typeof(DemoFixtureLoader).Assembly.GetManifestResourceStream(fileName);
        if (stream is null)
        {
            Log.Warning("DEMO_MODE: {FileName} not found as an embedded resource", fileName);
            return null;
        }

        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        if (transformJson is not null) json = transformJson(json);
        var scores = JsonSerializer.Deserialize<EspnScores>(json, EspnApiServiceJsonConverter.Settings);
        Log.Information("DEMO_MODE: Loaded {Count} events from {FileName}", scores?.Events?.Length ?? 0, fileName);
        return scores;
    }
}
