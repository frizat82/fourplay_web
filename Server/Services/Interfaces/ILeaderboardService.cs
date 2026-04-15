using FourPlayWebApp.Server.Models.Data;

namespace FourPlayWebApp.Server.Services.Interfaces;

public interface ILeaderboardService {
    public Task<List<LeaderboardModel>> BuildLeaderboard(int leagueId, long seasonYear);
    public Task<List<LeaderboardModel>> CalculateUserTotals(List<LeaderboardModel> leaderboard, int leagueId, long seasonYear, int maxWeek);
}
