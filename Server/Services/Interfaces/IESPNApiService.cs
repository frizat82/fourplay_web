using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Server.Services.Interfaces;

public interface IEspnApiService {
    public Task<EspnScores?> GetWeekScores(int week, int year, bool postSeason = false);
    public Task<EspnScores?> GetSeasonScores(int year);
}
