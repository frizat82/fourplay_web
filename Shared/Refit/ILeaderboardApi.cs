using FourPlayWebApp.Shared.Models.Dtos;
using Refit;

namespace FourPlayWebApp.Shared.Refit;

public interface ILeaderboardApi
{
    [Get("/api/Leaderboard/{leagueId}/leaderboard/{seasonYear}")]
    Task<ApiResponse<List<LeaderboardDto>>> GetLeaderboard(int leagueId, long seasonYear);

}
