using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Shared.Models.Data;

namespace FourPlayWebApp.Server.Services.Repositories.Interfaces;

public interface ICfbRepository : ISpreadRepository<CfbSpreads> {
    // A slate is an in-scope CfbSeasonWeekConfigs row (frizat-b4g) — these return only those.
    Task<IEnumerable<CfbSeasonWeekConfig>> GetSlatesForSeasonAsync(int season);
    Task<IEnumerable<CfbSeasonWeekConfig>> GetAllSlatesAsync();
    Task<CfbSeasonWeekConfig?> GetSlateByIdAsync(int slateId);
    Task UpsertCfbScoresAsync(IEnumerable<CfbScores> scores);
    Task<IEnumerable<CfbSpreads>> GetSpreadsForSlateAsync(int cfbSlateId);
    Task<IEnumerable<CfbScores>> GetScoresForSlateAsync(int cfbSlateId);
    /// <summary>Every slate's spreads / scores for a season in one query each — the leaderboard's source (NFL's shape).</summary>
    Task<List<CfbSpreads>> GetLeagueEligibleSpreadsForSeasonAsync(int season);
    Task<List<CfbScores>> GetScoresForSeasonAsync(int season);
    Task AddWeekConfigsAsync(IEnumerable<CfbSeasonWeekConfig> configs);
    Task AddRankingsAsync(IEnumerable<CfbRanking> rankings);
    Task<Dictionary<string, int>> GetLatestRankingsForWeekAsync(int season, int weekNumber);
}
