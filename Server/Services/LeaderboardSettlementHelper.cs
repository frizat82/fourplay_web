using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Shared.Models.Enum;

namespace FourPlayWebApp.Server.Services;

// Shared by CfbLeaderboardService.CalculateTotals and LeaderboardService.CalculateUserTotals
// (CLAUDE.md: NFL/CFB business logic lives in one shared function, never two copies) — a week
// can't be settled (paid out) while any user's fate for it is still pending a final score.
public static class LeaderboardSettlementHelper {
    public static bool IsWeekPending(List<LeaderboardModel> leaderboard, int weekIndex) =>
        leaderboard.Any(u => u.WeekResults[weekIndex].WeekResult == WeekResult.MissingGameResults);
}
