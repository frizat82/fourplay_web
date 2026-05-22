using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Server.Services.Interfaces;

public interface IEspnApiService {
    public Task<EspnScores?> GetWeekScores(int week, int year, bool postSeason = false);
    public Task<EspnScores?> GetSeasonScores(int year);
    public Task<EspnScores?> GetScores();
    /// <summary>
    /// Fetches live CFB game data from ESPN for a specific date range (matching a slate window).
    /// Returns the same EspnScores shape so the frontend can reuse nflAdapter patterns.
    /// </summary>
    public Task<EspnScores?> GetCfbScores(DateOnly startDate, DateOnly endDate);
}
