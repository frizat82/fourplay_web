using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Helpers;
using FourPlayWebApp.Shared.Models;

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
        _scores = DemoFixtureLoader.Load(env, "sample_espn_nfl.json", json =>
        {
            foreach (var map in NflTeamMappingHelpers.NflTeamAbbrMapping)
                json = json.Replace($"\"{map.Key}\"", $"\"{map.Value}\"");
            return json;
        });
    }

    public event Action? ScoresChanged; // never fired — demo data is static

    public Task<EspnScores?> GetScoresAsync() => Task.FromResult(_scores);
}
