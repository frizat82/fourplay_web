using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Server.Services;

/// <summary>
/// Demo-only implementation of ICfbCacheService that serves frozen ESPN data (including real
/// down/distance/field-position) from sample_espn_cfb.json — mirrors DemoEspnCacheService's role
/// for NFL. Only registered when DEMO_MODE=true. Never used in dev or prod.
/// </summary>
public class DemoCfbCacheService : ICfbCacheService
{
    private readonly EspnScores? _scores;

    public DemoCfbCacheService(IWebHostEnvironment env)
    {
        _scores = DemoFixtureLoader.Load(env, "sample_espn_cfb.json");
    }

    public event Action? ScoresChanged; // never fired — demo data is static

    public Task<EspnScores?> GetScoresAsync() => Task.FromResult(_scores);
}
