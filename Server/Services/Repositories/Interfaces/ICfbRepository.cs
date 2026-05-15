using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Shared.Models.Data;

namespace FourPlayWebApp.Server.Services.Repositories.Interfaces;

public interface ICfbRepository {
    Task<bool> SlatesExistForSeasonAsync(int season);
    Task AddSlatesAsync(IEnumerable<CfbSlates> slates);
    Task<IEnumerable<CfbSlates>> GetSlatesForSeasonAsync(int season);
    Task AddCfbSpreadsAsync(IEnumerable<CfbSpreads> spreads);
    Task UpsertCfbScoresAsync(IEnumerable<CfbScores> scores);
    Task<IEnumerable<CfbSpreads>> GetSpreadsForSlateAsync(int cfbSlateId);
    Task<IEnumerable<CfbScores>> GetScoresForSlateAsync(int cfbSlateId);
}
