using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Server.Services.Interfaces;
public interface IEspnCacheService
{
    Task<EspnScores?> GetScoresAsync();
    // (season, nflWeek) — our own internal NflSeasonWeekConfig.WeekId, never ESPN's own week
    // numbering (frizat-3nv: the caller no longer needs to know ESPN's numbering exists at all).
    Task<EspnScores?> GetWeekScoresAsync(int season, int nflWeek);
    // Evicts the cached historical reconstruction for one (season, internal NflWeek) so a fresh
    // NflScoresJob upsert is visible immediately instead of waiting for a process restart —
    // mirrors ICfbLiveScoreFetcher.InvalidateSlateCache.
    void InvalidateWeekCache(int season, int week);
    event Action? ScoresChanged;
}
