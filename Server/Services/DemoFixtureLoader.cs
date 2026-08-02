using System.Text.Json;
using FourPlayWebApp.Shared.Models;
using Serilog;

namespace FourPlayWebApp.Server.Services;

/// <summary>
/// Shared "read a captured ESPN fixture from the repo root" logic for the demo-mode cache
/// services (DemoEspnCacheService, DemoCfbCacheService) — kept as one implementation per
/// CLAUDE.md's rule against hand-synced NFL/CFB forks, which has repeatedly drifted into bugs.
/// </summary>
internal static class DemoFixtureLoader
{
    public static EspnScores? Load(IWebHostEnvironment env, string fileName, Func<string, string>? transformJson = null)
    {
        var path = Path.Combine(env.ContentRootPath, "..", fileName);
        if (!File.Exists(path))
        {
            Log.Warning("DEMO_MODE: {FileName} not found at {Path}", fileName, path);
            return null;
        }

        var json = File.ReadAllText(path);
        if (transformJson is not null) json = transformJson(json);
        var scores = JsonSerializer.Deserialize<EspnScores>(json, EspnApiServiceJsonConverter.Settings);
        Log.Information("DEMO_MODE: Loaded {Count} events from {FileName}", scores?.Events?.Length ?? 0, fileName);
        return scores;
    }
}
