using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Server.Services.Interfaces;

public interface ICfbApiService {
    Task<EspnScores?> GetScoresByDateAsync(DateOnly date);
    Task<EspnScores?> GetTop25ByDateAsync(DateOnly date);
    Task<EspnScores?> GetScoresByWeekAsync(int week, bool isPostSeason);
}
