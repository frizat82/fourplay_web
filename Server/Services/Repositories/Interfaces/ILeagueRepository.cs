using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Shared.Models.Data;

namespace FourPlayWebApp.Server.Services.Repositories.Interfaces;
public interface ILeagueRepository {
    // League and User related methods
    Task<List<LeagueUserMapping>> GetLeagueUserMappingsAsync(int leagueId);
    Task<List<LeagueUserMapping>> GetLeagueUserMappingsAsync(ApplicationUser user);
    Task<List<LeagueJuiceMapping>> GetLeagueJuiceMappingAsync(int leagueId);
    Task<LeagueJuiceMapping?> GetLeagueJuiceMappingAsync(int leagueId, int season);
    Task<LeagueInfo> GetLeagueInfoAsync(int leagueId);
    Task<List<ApplicationUser>> GetUsersAsync();
    Task<LeagueInfo?> GetLeagueByNameAsync(string leagueName);

    // NFL Weeks
    Task UpsertNflWeeksAsync(List<NflWeeks> weeks);
    Task<List<NflWeeks>> GetNflWeeksAsync(int season);

    // NFL Scores and Spreads
    Task AddNewNflSpreadsAsync(List<NflSpreads> spreads);
    Task UpsertNflScoresAsync(List<NflScores> scores);
    Task<List<NflScores>> GetNflScoresAsync(int season, int week);
    Task<List<NflScores>> GetAllNflScoresForSeasonAsync(int season);
    Task<List<NflSpreads>?> GetNflSpreadsAsync(int season, int week);
    Task<List<NflSpreads>> GetAllNflSpreadsForSeasonAsync(int season);

    // NFL Picks
    Task<List<NflPicks>> GetNflPicksAsync(int leagueId, int season, int week);
    Task<List<NflPicks>> GetUserNflPicksAsync(string userId, int leagueId, int season, int week);

    // Add operations
    Task AddLeagueUserAsync(LeagueUsers leagueUser);
    Task AddLeagueUserMappingAsync(LeagueUserMapping mapping);
    Task AddLeagueInfoAsync(LeagueInfo leagueInfo);
    Task AddLeagueJuiceMappingAsync(LeagueJuiceMapping mapping);
    Task AddNflScoresAsync(IEnumerable<NflScores> scores);
    Task AddNflSpreadsAsync(IEnumerable<NflSpreads> spreads);
    Task AddNflPicksAsync(IEnumerable<NflPicks> picks);

    // Remove operations
    Task RemoveNflScoresAsync(IEnumerable<NflScores> scores);
    Task RemoveNflSpreadsAsync(IEnumerable<NflSpreads> spreads);
    Task RemoveNflPicksAsync(IEnumerable<NflPicks> picks);

    // Utility methods
    Task<bool> LeagueExistsAsync(string leagueName, int season);
    Task<bool> LeagueExistsAsync(string leagueName);
    Task<bool> UserExistsInLeagueAsync(string userId, int leagueId);
}
