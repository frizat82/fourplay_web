using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Server.Services.Interfaces;
public interface IEspnCacheService
{
    Task<EspnScores?> GetScoresAsync();
    Task<EspnScores?> GetWeekScoresAsync(int week, int year, bool postSeason = false);
    // Evicts the cached historical reconstruction for one (season, internal NflWeek) so a fresh
    // NflScoresJob upsert is visible immediately instead of waiting for a process restart —
    // mirrors ICfbLiveScoreFetcher.InvalidateSlateCache.
    void InvalidateWeekCache(int season, int week);
    event Action? ScoresChanged;
}
