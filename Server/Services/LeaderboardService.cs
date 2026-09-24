using FourPlayWebApp.Server.Models.Data;
using Serilog;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models.Enum;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Helpers;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models.Data;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace FourPlayWebApp.Server.Services;

public class LeaderboardService(
    ILogger<LeaderboardService> logger,
    ILeagueRepository leagueRepository,
    TimeProvider timeProvider)
    : ILeaderboardService {


    private async Task<List<LeaderboardModel>> InteralLeaderboard(int leagueId, long seasonYear) {
        var leaderboard = new List<LeaderboardModel>();
        logger.LogDebug("Loading Scoreboard {LeagueId}", leagueId);
        try {
            var leagueUsers = await leagueRepository.GetLeagueUserMappingsAsync(leagueId);
            var leagueScores = await leagueRepository.GetAllNflScoresForSeasonAsync((int)seasonYear);
            var leagueSpreads = await leagueRepository.GetAllNflSpreadsForSeasonAsync((int)seasonYear);
            var leagueInfo = await leagueRepository.GetLeagueJuiceMappingAsync(leagueId);
            var seasonJuice = leagueInfo.FirstOrDefault(x => x.Season == seasonYear);

            if (seasonJuice is null) {
                logger.LogError("League info not found.");
                return leaderboard;
            }
            if (leagueScores.Count == 0 || leagueUsers.Count == 0)
                return leaderboard;

            // One query for every member's picks all season, looked up per (user, week) — not one
            // query per member × week (a 25-member league at week 18 was 450 round trips).
            var picksByUserWeek = (await leagueRepository.GetLeagueNflPicksForSeasonAsync(leagueId, (int)seasonYear))
                .ToLookup(p => (p.UserId, p.NflWeek), p => new PickRow(p.Team, p.Pick));

            var maxWeek = leagueScores.Max(x => x.NflWeek);
            var spreadsByWeek = leagueSpreads.ToLookup(s => s.NflWeek);
            var scoresByWeek = leagueScores.ToLookup(s => s.NflWeek);
            var weeks = Enumerable.Range(1, maxWeek)
                .Select(week => new LeaderboardPeriod(week, spreadsByWeek[week].ToList<IOddsRow>(), scoresByWeek[week].ToList<IScoreRow>()))
                .ToList();

            leaderboard = LeaderboardEngine.Build(LeagueType.Nfl, leagueUsers, weeks,
                (userId, week) => picksByUserWeek[(userId, week)], seasonJuice, timeProvider.GetUtcNow(),
                (pick, week, ex) => logger.LogError(ex, "Error scoring pick {@Pick} week {Week}", pick, week));
        } catch (Exception ex) {
            logger.LogError(ex, "Error loading leaderboard");
            return leaderboard;
        }

        return leaderboard;
    }


    public async Task<List<LeaderboardModel>> BuildLeaderboard(int leagueId, long seasonYear) {
        if (leagueId != 0) {
            return await InteralLeaderboard(leagueId, seasonYear);
        }

        logger.LogError("League ID is not set.");
        return [];
    }

    public async Task<List<LeaderboardModel>> CalculateUserTotals(List<LeaderboardModel> leaderboard, int leagueId,
        long seasonYear, int maxWeek) {
        logger.LogDebug("Loading User Totals");
        var leagueJuice = await leagueRepository.GetLeagueJuiceMappingAsync(leagueId, (int)seasonYear);
        if (leagueJuice is null || leagueJuice.WeeklyCost == 0) {
            logger.LogError("League Juice mapping not found or weekly cost is zero.");
            return leaderboard;
        }

        return LeaderboardSettlementHelper.SettleWeeks(leaderboard, leagueJuice.WeeklyCost, maxWeek, leagueJuice.StartWeek);
    }

}
