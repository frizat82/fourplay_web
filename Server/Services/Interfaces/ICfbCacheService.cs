using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Server.Services.Interfaces;

/// <summary>
/// Mirrors IEspnCacheService's shape exactly (frizat-703.6 unification) — same flows, same pages,
/// differing only in what/how each sport fetches (ICfbLiveScoreFetcher's CFP/ranked-team logic
/// vs NFL's plain week query).
/// </summary>
public interface ICfbCacheService
{
    Task<EspnScores?> GetScoresAsync();
    // Live/settled CFB scores for a SPECIFIC (typically non-current) slate — mirrors
    // IEspnCacheService.GetWeekScoresAsync(season, nflWeek) exactly (frizat-d0t unification):
    // settled slates are served from a cached DB reconstruction, everything else live-fetched.
    Task<EspnScores?> GetSlateScoresAsync(int slateId);
    // Evicts the cached settled-slate reconstruction so a fresh CfbScoresJob upsert is visible
    // immediately instead of waiting for a process restart — mirrors
    // IEspnCacheService.InvalidateWeekCache.
    void InvalidateSlateCache(int slateId);
    event Action? ScoresChanged;
}
