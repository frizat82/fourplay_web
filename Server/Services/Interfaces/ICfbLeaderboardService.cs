using FourPlayWebApp.Server.Models.Data;

namespace FourPlayWebApp.Server.Services.Interfaces;

public interface ICfbLeaderboardService {
    Task<List<LeaderboardModel>> BuildLeaderboard(int leagueId, int season);
}
