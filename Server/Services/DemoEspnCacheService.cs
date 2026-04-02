using System.Text.Json;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models;
using Serilog;

namespace FourPlayWebApp.Server.Services;

/// <summary>
/// Demo-only implementation of IEspnCacheService that serves frozen ESPN data
/// from sample_espn_nfl.json. Only registered when DEMO_MODE=true.
/// Never used in dev or prod.
/// </summary>
public class DemoEspnCacheService : IEspnCacheService
{
    private readonly EspnScores? _scores;

    public DemoEspnCacheService(IWebHostEnvironment env)
    {
        var path = Path.Combine(env.ContentRootPath, "..", "sample_espn_nfl.json");
        if (!File.Exists(path))
        {
            Log.Warning("DEMO_MODE: sample_espn_nfl.json not found at {Path}", path);
            return;
        }

        var json = File.ReadAllText(path);
        _scores = JsonSerializer.Deserialize<EspnScores>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        Log.Information("DEMO_MODE: Loaded {Count} events from sample_espn_nfl.json",
            _scores?.Events?.Length ?? 0);
    }

    public Task<EspnScores?> GetScoresAsync() => Task.FromResult(_scores);
}
