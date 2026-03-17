using FourPlayWebApp.Server.Models.Data;
using Microsoft.Extensions.Caching.Memory;
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
    IServiceScopeFactory scopeFactory,
    ILeagueRepository leagueRepository)
    : ILeaderboardService {
    

    private async Task<List<LeaderboardModel>> InteralLeaderboard(int leagueId, long seasonYear) {
        var leaderboard = new List<LeaderboardModel>();
        logger.LogDebug("Loading Scoreboard {LeagueId}", leagueId);
        try {
            var leagueUsers = await leagueRepository.GetLeagueUserMappingsAsync(leagueId);
            var leagueScores = await leagueRepository.GetAllNflScoresForSeasonAsync((int)seasonYear);
            var leagueInfo = await leagueRepository.GetLeagueJuiceMappingAsync(leagueId);

            if (leagueInfo.Count == 0 || leagueInfo.All(x => x.Season != seasonYear)) {
                logger.LogError("League info not found.");
                return leaderboard;
            }
            if (leagueScores.Count == 0 || leagueUsers.Count == 0)
                return leaderboard;

            var maxWeek = leagueScores.Max(x => x.NflWeek);
            foreach (var user in leagueUsers) {
                var userPoints = new LeaderboardModel {
                    WeekResults = new LeaderboardWeekResults[maxWeek],
                    User = user.User
                };
                for (int week = 1; week <= maxWeek; week++) {
                    var weekResult = await CalculatePicks(leagueId, seasonYear, leagueScores, user, week);
                    userPoints.WeekResults[week - 1] = weekResult;
                }
                leaderboard.Add(userPoints);
            }
            leaderboard = await CalculateUserTotals(leaderboard, leagueId, seasonYear, maxWeek);
        } catch (Exception ex) {
            logger.LogError(ex, "Error loading leaderboard");
            return leaderboard;
        }

        return leaderboard;
    }
    

    private async Task<LeaderboardWeekResults> CalculatePicks(int leagueId, long seasonYear,
        List<NflScores> userScores, LeagueUserMapping user, int week) {
        var weekResult = new LeaderboardWeekResults {
            Week = week
        };
        
        await using var scope = scopeFactory.CreateAsyncScope();
        var spreadCalculatorBuilder = scope.ServiceProvider.GetRequiredService<ISpreadCalculatorBuilder>();
        var spreadCalculator = await spreadCalculatorBuilder.WithLeagueId(leagueId).WithWeek(week).WithSeason((int)seasonYear).BuildAsync();
        var userPicks = await leagueRepository.GetUserNflPicksAsync(user.UserId, leagueId, (int)seasonYear, week);
        var allPicksBeatSpread = userPicks.All(pick => {
            try {
                return IsPickAWinner(userScores, week, pick, spreadCalculator);
            } catch (Exception ex) {
                logger.LogError(ex, "Error calculating pick winner for user {User} week {Week} pick {@Pick}",
                    user.UserId, week, pick);
                return false;
            }
        });
        if (!allPicksBeatSpread) {
            weekResult.WeekResult = WeekResult.Lost; // Any loss is an immediate full week loss
        }
        else if (userPicks.Count < GameHelpers.GetRequiredPicks(week)) {
            logger.LogDebug("{User} {League} Missing Picks {Week} {Count} {Required}", user.User, user.League.LeagueName, week, userPicks.Count,
                GameHelpers.GetRequiredPicks(week));
            weekResult.WeekResult = WeekResult.MissingPicks;
        }
        else if (userPicks.Any(pick => {
                     var score = userScores.FirstOrDefault(s =>
                         s.NflWeek == week && (s.HomeTeam == pick.Team || s.AwayTeam == pick.Team));
                     return score is null;
                 })) {
            weekResult.WeekResult = WeekResult.MissingGameResults;
        }
        else {
            weekResult.WeekResult = allPicksBeatSpread ? WeekResult.Won : WeekResult.Lost;
        }

        return weekResult;
    }

    private bool IsPickAWinner(List<NflScores> userScores, int week, NflPicks pick, ISpreadCalculator spreadCalculator)
    {
        var score = userScores.FirstOrDefault(s => s.NflWeek == week && (s.HomeTeam == pick.Team || s.AwayTeam == pick.Team));
        if (score is null)
            return true; // you are a winner if there is no score
        var isHome = score!.HomeTeam == pick.Team;
        var isWinner = spreadCalculator.DidUserWinPick(pick.Team, isHome ? score.HomeTeamScore : score.AwayTeamScore, isHome ? score.AwayTeamScore : score.HomeTeamScore, pick.Pick);
        logger.LogDebug("{Week} Pick: {@Pick} Team: {Team} HomeScore: {HomeScore} AwayScore: {AwayScore} IsHome: {IsHome} IsWinner: {IsWinner}",
            week, "Spread", pick.Team, score.HomeTeamScore, score.AwayTeamScore, isHome, isWinner);
        return isWinner;
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

        var baseWeeklyCost = leagueJuice.WeeklyCost;
        var currentWeeklyCost = baseWeeklyCost;

        for (int week = 1; week <= maxWeek; week++) {
            var winners = new List<string>();
            var losers = new List<string>();
            
            foreach (var result in leaderboard) {
                var resultWeek = result.WeekResults.FirstOrDefault(w => w.Week == week);
                if (resultWeek is null) {
                    logger.LogError("Week result not found for week {Week} in leaderboard for user {User}", week, result.User.NormalizedUserName);
                    continue;
                }
                var userId = result.User.Id;
                if (resultWeek.WeekResult == WeekResult.Won) {
                    winners.Add(userId);
                }
                else {
                    losers.Add(userId);
                }
            }
            // If we have losers it means not everyone won this week
            if (winners.Count > 0 && losers.Count > 0) {
                foreach (var result in leaderboard) {
                    if (result.WeekResults[week - 1].WeekResult != WeekResult.Won) {
                        //Log.Information("User {User} lost week {Week} {Count} {Cost}", userId, week, winners.Count, currentWeeklyCost);
                        result.WeekResults[week - 1].Score = - (winners.Count * currentWeeklyCost);
                    }
                    else {
                        //Log.Information("User {User} won week {Week} {Count} {Winnings}", userId, week, winners.Count, currentWeeklyCost);
                        result.WeekResults[week - 1].Score = losers.Count * currentWeeklyCost;
                    }
                }
                currentWeeklyCost = baseWeeklyCost;
            }
            // Double the cost for the next week if all users won this week
            else {
                currentWeeklyCost += baseWeeklyCost;
                logger.LogDebug("All users won or lost week {Week} Doubling {Juice}", week, currentWeeklyCost);
                foreach (var result in leaderboard) {
                    result.WeekResults[week - 1].Score = 0;
                }
            }
        }

        foreach (var result in leaderboard) {
            result.Total = result.WeekResults.Sum(w => w.Score);
        }

        return ComputeRanks(leaderboard);
    }
    private static List<LeaderboardModel> ComputeRanks(List<LeaderboardModel> leaderboardModel)
    {
        var ordered = leaderboardModel
            .OrderByDescending(x => x.Total)
            .ToList();

        int currentRank = 1;
        int skipped = 0;
        long? lastScore = null;

        for (int i = 0; i < ordered.Count; i++)
        {
            var player = ordered[i];

            if (lastScore == null || player.Total != lastScore)
            {
                // New rank
                currentRank = i + 1;
                skipped = 0;
            }
            else
            {
                // Tie → same rank, mark as "T"
                skipped++;
            }

            player.Rank = skipped > 0 ? $"T{currentRank}" : currentRank.ToString();
            lastScore = player.Total;
        }

        // Update your bound collection
        return ordered;
    }

}