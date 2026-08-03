using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Server.Services;

// Service that periodically refreshes NFL scores from ESPN and caches them in memory, so
// concurrent controller requests share one poll instead of each triggering its own ESPN call.
// Wraps the shared PeriodicRefreshCache engine (frizat-703.6) — CfbCacheService uses the same
// engine for CFB, differing only in what/how it fetches.
public class EspnCacheService : IEspnCacheService, IAsyncDisposable
{
    private readonly PeriodicRefreshCache<EspnScores> _cache;

    public event Action? ScoresChanged
    {
        add => _cache.Changed += value;
        remove => _cache.Changed -= value;
    }

    public EspnCacheService(IEspnApiService espnApiService, INflCurrentWeekService nflCurrentWeekService, TimeSpan? initialDelay = null)
    {
        _cache = new PeriodicRefreshCache<EspnScores>(
            fetch: async () => {
                var week = await nflCurrentWeekService.GetCurrentWeekAsync();
                return await espnApiService.GetWeekScores(week.EspnWeek, week.Season, week.IsPostSeason);
            },
            fingerprint: EspnScoresFingerprint.Compute,
            interval: TimeSpan.FromMinutes(5),
            initialDelay: initialDelay);
    }

    public Task<EspnScores?> GetScoresAsync() => Task.FromResult(_cache.Current);

    public ValueTask DisposeAsync() => _cache.DisposeAsync();
}
