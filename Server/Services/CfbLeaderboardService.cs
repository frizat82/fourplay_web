using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Helpers;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Enum;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace FourPlayWebApp.Server.Services;

public class CfbLeaderboardService(
    ILogger<CfbLeaderboardService> logger,
    ILeagueRepository leagueRepository,
    ICfbRepository cfbRepository,
    ICfbPicksRepository cfbPicksRepository)
    : ICfbLeaderboardService {

    public async Task<List<LeaderboardModel>> BuildLeaderboard(int leagueId, int season) {
        var leaderboard = new List<LeaderboardModel>();
        if (leagueId == 0) return leaderboard;

        try {
            var leagueUsers = await leagueRepository.GetLeagueUserMappingsAsync(leagueId);
            var juiceMapping = await leagueRepository.GetLeagueJuiceMappingAsync(leagueId, season);
            var slates = (await cfbRepository.GetSlatesForSeasonAsync(season))
                .OrderBy(s => s.SlateNumber).ToList();

            if (juiceMapping is null || leagueUsers.Count == 0 || slates.Count == 0)
                return leaderboard;

            foreach (var user in leagueUsers) {
                var userModel = new LeaderboardModel {
                    User = user.User,
                    WeekResults = new LeaderboardWeekResults[slates.Count],
                };

                for (int i = 0; i < slates.Count; i++) {
                    var slate = slates[i];
                    var spreads = (await cfbRepository.GetSpreadsForSlateAsync(slate.Id)).ToList();
                    var scores = (await cfbRepository.GetScoresForSlateAsync(slate.Id)).ToList();
                    var picks = (await cfbPicksRepository.GetUserPicksAsync(leagueId, slate.Id, user.UserId)).ToList();
                    var juice = JuiceForSlate(slate.SlateNumber, juiceMapping);

                    userModel.WeekResults[i] = EvaluateSlate(slate.SlateNumber, spreads, scores, picks, juice, user.UserId);
                }

                leaderboard.Add(userModel);
            }

            leaderboard = CalculateTotals(leaderboard, juiceMapping, slates.Count);
        } catch (Exception ex) {
            logger.LogError(ex, "Error building CFB leaderboard for league {LeagueId}", leagueId);
        }

        return leaderboard;
    }

    private static double JuiceForSlate(int slateNumber, LeagueJuiceMapping juice) => slateNumber switch {
        <= 14 => juice.Juice,
        <= 17 => juice.JuiceDivisional,
        _ => juice.JuiceConference,  // slates 18 (Semifinals) and 19 (Championship)
    };

    private LeaderboardWeekResults EvaluateSlate(int slateNumber, List<CfbSpreads> spreads, List<CfbScores> scores,
        List<CfbPicks> picks, double juice, string userId) {
        var result = new LeaderboardWeekResults { Week = slateNumber };

        if (picks.Count == 0 && spreads.Count > 0) {
            result.WeekResult = WeekResult.MissingPicks;
            return result;
        }

        var allWon = picks.All(pick => {
            try {
                return DidPickWin(pick, spreads, scores, juice);
            } catch (Exception ex) {
                logger.LogError(ex, "Error evaluating CFB pick {@Pick}", pick);
                return false;
            }
        });

        if (!allWon) {
            result.WeekResult = WeekResult.Lost;
        } else if (picks.Count < GameHelpers.GetCfbRequiredPicks(slateNumber)) {
            result.WeekResult = WeekResult.MissingPicks;
        } else if (picks.Any(pick => !scores.Any(s => s.HomeTeam == pick.Team || s.AwayTeam == pick.Team))) {
            result.WeekResult = WeekResult.MissingGameResults;
        } else {
            result.WeekResult = WeekResult.Won;
        }

        return result;
    }

    private static bool DidPickWin(CfbPicks pick, List<CfbSpreads> spreads, List<CfbScores> scores, double juice) {
        var spread = spreads.FirstOrDefault(s => s.HomeTeam == pick.Team || s.AwayTeam == pick.Team);
        if (spread is null) return true;

        var score = scores.FirstOrDefault(s => s.HomeTeam == pick.Team || s.AwayTeam == pick.Team);
        if (score is null) return true; // game not yet scored

        var isHome = spread.HomeTeam == pick.Team;
        var teamScore = isHome ? score.HomeTeamScore : score.AwayTeamScore;
        var otherScore = isHome ? score.AwayTeamScore : score.HomeTeamScore;
        var rawSpread = isHome ? spread.HomeTeamSpread : spread.AwayTeamSpread;

        return pick.PickType switch {
            "Spread" => teamScore + rawSpread + juice - otherScore > 0,
            "Over" => teamScore + otherScore > spread.OverUnder - juice,
            "Under" => teamScore + otherScore < spread.OverUnder + juice,
            _ => false,
        };
    }

    private static List<LeaderboardModel> CalculateTotals(List<LeaderboardModel> leaderboard,
        LeagueJuiceMapping juiceMapping, int slateCount) {
        var baseWeeklyCost = juiceMapping.WeeklyCost;
        var currentWeeklyCost = baseWeeklyCost;

        for (int i = 0; i < slateCount; i++) {
            var winners = leaderboard.Where(u => u.WeekResults[i].WeekResult == WeekResult.Won).Select(u => u.User.Id).ToList();
            var losers = leaderboard.Where(u => u.WeekResults[i].WeekResult != WeekResult.Won).Select(u => u.User.Id).ToList();

            if (winners.Count > 0 && losers.Count > 0) {
                foreach (var user in leaderboard) {
                    user.WeekResults[i].Score = user.WeekResults[i].WeekResult == WeekResult.Won
                        ? losers.Count * currentWeeklyCost
                        : -(winners.Count * currentWeeklyCost);
                }
                currentWeeklyCost = baseWeeklyCost;
            } else {
                currentWeeklyCost += baseWeeklyCost;
                foreach (var user in leaderboard)
                    user.WeekResults[i].Score = 0;
            }
        }

        foreach (var user in leaderboard)
            user.Total = user.WeekResults.Sum(w => w.Score);

        return ComputeRanks(leaderboard);
    }

    private static List<LeaderboardModel> ComputeRanks(List<LeaderboardModel> leaderboard) {
        var ordered = leaderboard.OrderByDescending(x => x.Total).ToList();
        int currentRank = 1;
        int skipped = 0;
        long? lastScore = null;

        for (int i = 0; i < ordered.Count; i++) {
            var player = ordered[i];
            if (lastScore == null || player.Total != lastScore) {
                currentRank = i + 1;
                skipped = 0;
            } else {
                skipped++;
            }
            player.Rank = skipped > 0 ? $"T{currentRank}" : currentRank.ToString();
            lastScore = player.Total;
        }

        return ordered;
    }
}
