using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Server.Services.Interfaces;

public interface ICfbApiService {
    Task<EspnScores?> GetScoresByDateRangeAsync(DateOnly startDate, DateOnly endDate);
    Task<EspnScores?> GetCfpGamesAsync();
    // One ESPN day bucket — what a live poll asks for (LiveDayRefresh).
    Task<EspnScores?> GetScoresForDayAsync(DateOnly date);
}
