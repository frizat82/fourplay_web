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
    event Action? ScoresChanged;
}
