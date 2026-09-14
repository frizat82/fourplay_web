using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Enum;

namespace FourPlayWebApp.Server.Services.Repositories.Interfaces;
public interface ILeagueRepository : ISpreadRepository<NflSpreads> {
    // League and User related methods
    Task<List<LeagueUserMapping>> GetLeagueUserMappingsAsync(int leagueId);
    Task<List<LeagueUserMapping>> GetLeagueUserMappingsAsync(ApplicationUser user);
    Task<List<LeagueJuiceMapping>> GetLeagueJuiceMappingAsync(int leagueId);
    Task<LeagueJuiceMapping?> GetLeagueJuiceMappingAsync(int leagueId, int season);
    Task<LeagueInfo> GetLeagueInfoAsync(int leagueId);
    Task<List<ApplicationUser>> GetUsersAsync();
    Task<LeagueInfo?> GetLeagueByNameAsync(string leagueName);

    // NFL Season Week Config
    Task<List<NflSeasonWeekConfig>> GetNflSeasonWeekConfigsAsync();
    // Season-scoped, mirrors ICfbRepository.GetSlatesForSeasonAsync — for callers (NflScoresJob)
    // that must never sweep every season on record, only the one currently in play.
    Task<List<NflSeasonWeekConfig>> GetNflSeasonWeekConfigsAsync(int season);

    // NFL Weeks
    Task UpsertNflWeeksAsync(List<NflWeeks> weeks);
    Task<List<NflWeeks>> GetNflWeeksAsync(int season);

    // NFL Scores and Spreads
    Task UpsertNflScoresAsync(List<NflScores> scores);
    Task<List<NflScores>> GetNflScoresAsync(int season, int week);
    Task<List<NflScores>> GetAllNflScoresForSeasonAsync(int season);
    Task<List<NflSpreads>?> GetNflSpreadsAsync(int season, int week);
    Task<List<NflSpreads>> GetAllNflSpreadsForSeasonAsync(int season);

    // NFL Picks
    Task<List<NflPicks>> GetNflPicksAsync(int leagueId, int season, int week);
    Task<List<NflPicks>> GetUserNflPicksAsync(string userId, int leagueId, int season, int week);

    // Commissioner portal methods
    Task<List<LeagueInfo>> GetLeaguesByOwnerAsync(string ownerId);
    Task<List<LeagueInfo>> GetAllLeaguesAsync();
    Task UpdateLeagueOwnerAsync(int leagueId, string newOwnerUserId);
    Task UpdateLeagueJuiceMappingAsync(LeagueJuiceMapping mapping);
    Task RemoveLeagueUserMappingAsync(int leagueId, string userId);
    Task<int> GetLeagueMemberCountAsync(int leagueId);
    // Season-aware: counts members whose (DateCreated, RemovedAt) membership window overlapped
    // the given season's date range for that league's sport — not just currently-active members.
    Task<int> GetLeagueMemberCountAsync(int leagueId, int season, LeagueType leagueType);
    Task<Dictionary<int, int>> GetLeagueMemberCountsAsync(int season);
    Task DeleteLeagueAsync(int leagueId);
    Task<HashSet<(int LeagueId, int Season)>> GetJuiceRemindersSentAsync();

    // Add operations
    Task AddLeagueUserMappingAsync(LeagueUserMapping mapping);
    Task<LeagueInfo> AddLeagueInfoAsync(LeagueInfo leagueInfo);
    Task AddLeagueJuiceMappingAsync(LeagueJuiceMapping mapping);
    Task RecordJuiceReminderSentAsync(int leagueId, int season);
    Task AddNflScoresAsync(IEnumerable<NflScores> scores);
    Task AddNflSpreadsAsync(IEnumerable<NflSpreads> spreads);
    Task AddNflPicksAsync(IEnumerable<NflPicks> picks);

    // Atomically checks the (user, league, season, week) pick count against requiredPicks and
    // inserts newPicks only if it still fits, all under one advisory-lock-held transaction —
    // see PickConcurrencyGuard. Returns false (nothing written) if the cap would be exceeded.
    Task<bool> TryAddNflPicksAsync(IEnumerable<NflPicks> newPicks, string userId, int leagueId, int season, int week, int requiredPicks);

    // Removes a single pick (identified by its natural key, not Id) for the authenticated user,
    // under the same advisory lock TryAddNflPicksAsync uses — so an add and a remove for the same
    // (user, league, season, week) can never interleave unsafely. Idempotent: returns false (no-op)
    // if no matching pick exists, rather than throwing.
    Task<bool> TryRemoveNflPickAsync(string userId, int leagueId, int season, int week, string team, PickType pickType);

    // Remove operations
    Task RemoveNflScoresAsync(IEnumerable<NflScores> scores);
    Task RemoveNflSpreadsAsync(IEnumerable<NflSpreads> spreads);
    Task RemoveNflPicksAsync(IEnumerable<NflPicks> picks);

    // Utility methods
    Task<bool> LeagueExistsAsync(string leagueName, int season);
    Task<bool> LeagueExistsAsync(string leagueName);
    Task<bool> UserExistsInLeagueAsync(string userId, int leagueId);
}
