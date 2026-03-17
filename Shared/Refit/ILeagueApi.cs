using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Data.Dtos;
using FourPlayWebApp.Shared.Models.Enum;
using Refit;

namespace FourPlayWebApp.Shared.Refit;

public interface ILeagueApi
{
    // League Info
    [Get("/api/league/{leagueId}")]
    Task<ApiResponse<LeagueInfoDto>> GetLeagueInfo(int leagueId);

    [Get("/api/league/by-name/{leagueName}")]
    Task<ApiResponse<LeagueInfoDto?>> GetLeagueByName(string leagueName);

    // League Juice
    [Get("/api/league/{leagueId}/juice")]
    Task<ApiResponse<List<LeagueJuiceMappingDto>>> GetLeagueJuice(int leagueId);

    [Get("/api/league/{leagueId}/juice/{season}")]
    Task<ApiResponse<LeagueJuiceMappingDto?>> GetLeagueJuiceForSeason(int leagueId, int season);

    // League Users / Mappings
    [Get("/api/league/{leagueId}/users")]
    Task<ApiResponse<List<LeagueUserMappingDto>>> GetLeagueUserMappings(int leagueId);

    [Get("/api/league/user-mappings/by-user/{userId}")]
    Task<ApiResponse<List<LeagueUserMappingDto>>> GetLeagueUserMappingsForUser(string userId);

    [Get("/api/league/users")]
    Task<ApiResponse<List<UserSummaryDto>>> GetUsers();

    // NFL Weeks
    [Get("/api/league/weeks/{season}")]
    Task<ApiResponse<List<NflWeeksDto>>> GetNflWeeks(int season);

    // NFL Scores
    [Get("/api/league/scores/{season}/{week}")]
    Task<ApiResponse<List<NflScores>>> GetScores(int season, int week);

    [Get("/api/league/scores/{season}")]
    Task<ApiResponse<List<NflScores>>> GetScoresForSeason(int season);

    [Post("/api/league/scores/upsert")]
    Task<IApiResponse> UpsertScores([Body] List<NflScores> scores);

    [Post("/api/league/scores")]
    Task<IApiResponse> AddScores([Body] IEnumerable<NflScores> scores);

    [Delete("/api/league/scores")]
    Task<IApiResponse> RemoveScores([Body] IEnumerable<NflScores> scores);

    // NFL Spreads
    [Get("/api/league/spreads/{season}/{week}")]
    Task<ApiResponse<List<NflSpreads>?>> GetSpreads(int season, int week);

    [Get("/api/league/spreads/{season}")]
    Task<ApiResponse<List<NflSpreads>>> GetSpreadsForSeason(int season);

    [Post("/api/league/spreads/add-new")]
    Task<IApiResponse> AddNewSpreads([Body] List<NflSpreads> spreads);

    [Post("/api/league/spreads")]
    Task<IApiResponse> AddSpreads([Body] IEnumerable<NflSpreads> spreads);

    [Delete("/api/league/spreads")]
    Task<IApiResponse> RemoveSpreads([Body] IEnumerable<NflSpreads> spreads);

    // NFL Picks
    [Get("/api/league/{leagueId}/picks/{season}/{week}")]
    Task<ApiResponse<List<NflPickDto>>> GetLeaguePicks(int leagueId, int season, int week);

    [Get("/api/league/{leagueId}/picks/{season}/{week}/user/{userId}")]
    Task<ApiResponse<List<NflPickDto>>> GetUserPicks(string userId, int leagueId, int season, int week);

    [Post("/api/league/picks")]
    Task<ApiResponse<int>> AddPicks([Body] IEnumerable<NflPickDto> picks);

    [Delete("/api/league/picks")]
    Task<IApiResponse> RemovePicks([Body] IEnumerable<NflPickDto> picks);
    
    // Odds Calculations
    [Get("/api/league/{leagueId}/odds/{season}/{week}/didUserWin")]
    Task<ApiResponse<bool>> DidUserWin(
        int leagueId, int season, int week,
        string team, 
        int pickTeamScore, 
        int otherTeamScore);

    [Get("/api/league/{leagueId}/odds/{season}/{week}/team/{team}")]
    Task<ApiResponse<double?>> GetSpread(int leagueId, int season, int week, string team);

    [Get("/api/league/{leagueId}/odds/{season}/{week}/team/{team}/overunder")]
    Task<ApiResponse<double?>> GetOverUnder(
        int leagueId, 
        int season, 
        int week, 
        string team, 
        PickType pickType = PickType.Spread);

    [Get("/api/league/{leagueId}/odds/{season}/{week}/exists")]
    Task<ApiResponse<bool>> DoOddsExist(int leagueId, int season, int week);

    [Get("/api/league/{leagueId}/odds/{season}/{week}/juice")]
    Task<ApiResponse<double>> GetLeagueSpread(int leagueId, int season, int week);

    [Post("/api/league/{leagueId}/odds/{season}/{week}")]
    Task<ApiResponse<BatchSpreadResponse>> SpreadBatch(
        int leagueId, 
        int season, 
        int week, 
        [Body] BatchSpreadRequest request);
    [Post("/api/league/{leagueId}/odds/{season}/{week}/calculate-batch")]
    Task<ApiResponse<BatchSpreadCalculationResponse>> CalculateSpreadBatch(
        int leagueId, 
        int season, 
        int week, 
        [Body] BatchSpreadCalculationRequest request);

    // Utilities
    [Get("/api/league/exists/league/{leagueName}/{season}")]
    Task<bool> LeagueExists(string leagueName, int season);
    [Get("/api/league/exists/league/{leagueName}")]
    Task<bool> LeagueExists(string leagueName);

    [Get("/api/league/exists/user-in-league/{userId}/{leagueId}")]
    Task<bool> UserExistsInLeague(string userId, int leagueId);

    // Core Entity Operations
    [Post("/api/league/league-user")]
    Task AddLeagueUser([Body] LeagueUsersDto leagueUser);

    [Post("/api/league/league-user-mapping")]
    Task AddLeagueUserMapping([Body] LeagueUserMappingDto mapping);

    [Post("/api/league/league-info")]
    Task AddLeagueInfo([Body] LeagueInfoDto leagueInfo);

    [Post("/api/league/league-juice-mapping")]
    Task AddLeagueJuiceMapping([Body] LeagueJuiceMappingDto mapping);
}
