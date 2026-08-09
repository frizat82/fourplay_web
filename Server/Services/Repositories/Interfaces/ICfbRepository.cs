using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Shared.Models.Data;

namespace FourPlayWebApp.Server.Services.Repositories.Interfaces;

public interface ICfbRepository : ISpreadRepository<CfbSpreads> {
    Task<bool> SlatesExistForSeasonAsync(int season);
    Task AddSlatesAsync(IEnumerable<CfbSlates> slates);
    Task DeleteSlatesAsync(IEnumerable<CfbSlates> slates);
    Task<IEnumerable<CfbSlates>> GetSlatesForSeasonAsync(int season);
    Task<CfbSlates?> GetSlateByIdAsync(int slateId);
    Task UpsertCfbScoresAsync(IEnumerable<CfbScores> scores);
    Task<IEnumerable<CfbSpreads>> GetSpreadsForSlateAsync(int cfbSlateId);
    Task<IEnumerable<CfbScores>> GetScoresForSlateAsync(int cfbSlateId);
    Task<IEnumerable<CfbSeasonWeekConfig>> GetWeekConfigsForSeasonAsync(int season);
    Task<IEnumerable<CfbSeasonWeekConfig>> GetAllWeekConfigsAsync();
    Task AddWeekConfigsAsync(IEnumerable<CfbSeasonWeekConfig> configs);
    Task AddRankingsAsync(IEnumerable<CfbRanking> rankings);
    Task<HashSet<(int Season, int Week)>> GetWeeksWithSpreadDataAsync();
}
