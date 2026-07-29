using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Server.Services;

/// <summary>
/// Demo-only implementation of ICfbCacheService. Demo CFB "live" data comes entirely from
/// DemoDataSeeder-seeded CfbScores DB rows, never from live ESPN — mirrors DemoEspnCacheService's
/// role for NFL. Always returns null so the frontend's existing DB-fallback path in
/// buildGamesFromEspn takes over, exactly matching today's demo behavior. Only registered when
/// DEMO_MODE=true. Never used in dev or prod.
/// </summary>
public class DemoCfbCacheService : ICfbCacheService
{
    public event Action? ScoresChanged; // never fired — demo data is static

    public Task<EspnScores?> GetScoresAsync() => Task.FromResult<EspnScores?>(null);
}
