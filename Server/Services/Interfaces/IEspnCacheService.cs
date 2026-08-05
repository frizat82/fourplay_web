using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Server.Services.Interfaces;
public interface IEspnCacheService
{
    Task<EspnScores?> GetScoresAsync();
    Task<EspnScores?> GetWeekScoresAsync(int week, int year, bool postSeason = false);
    event Action? ScoresChanged;
}
