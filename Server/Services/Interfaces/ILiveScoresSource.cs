using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Server.Services.Interfaces;

/// <summary>
/// What both sports' live caches (IEspnCacheService, ICfbCacheService) share: the current
/// scoreboard, and an event raised from the cache's poll loop whenever it changes.
/// </summary>
public interface ILiveScoresSource
{
    Task<EspnScores?> GetScoresAsync();
    event Action? ScoresChanged;
}
