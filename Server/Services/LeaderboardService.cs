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

            // One query for every member's picks all season, looked up per (user, week) below —
            // not one query per member × week (a 25-member league at week 18 was 450 round trips).
            var picksByUserWeek = (await leagueRepository.GetLeagueNflPicksForSeasonAsync(leagueId, (int)seasonYear))
                .ToLookup(p => (p.UserId, p.NflWeek));

            var maxWeek = leagueScores.Max(x => x.NflWeek);
            // Captured/grouped once for the whole run rather than re-derived per user/week: "now"
            // stays consistent across every user (a wall-clock tick past a kickoff boundary mid-run
            // must not give different users a different verdict for the same week), and grouping
            // avoids re-filtering the full-season spread list on every one of the users × weeks
            // calls to CalculatePicks below.
            var now = timeProvider.GetUtcNow();
            var spreadsByWeek = leagueSpreads.ToLookup(s => s.NflWeek);
            // One calculator per week, shared by every member (each build costs cache lookups, and
            // the answer doesn't depend on who's asking). One DI scope for the whole run.
            await using var scope = scopeFactory.CreateAsyncScope();
            var spreadCalculatorBuilder = scope.ServiceProvider.GetRequiredService<ISpreadCalculatorBuilder>();
            var calculators = new Dictionary<int, ISpreadCalculator>();
            async Task<ISpreadCalculator> CalculatorFor(int week) {
                if (!calculators.TryGetValue(week, out var calc)) {
                    calc = await spreadCalculatorBuilder.WithLeagueId(leagueId).WithWeek(week).WithSeason((int)seasonYear).BuildAsync();
                    calculators[week] = calc;
                }
                return calc;
            }
            foreach (var user in leagueUsers) {
                var userPoints = new LeaderboardModel {
                    WeekResults = new LeaderboardWeekResults[maxWeek],
                    User = user.User
                };
                for (int week = 1; week <= maxWeek; week++) {
                    var weekResult = await CalculatePicks(leagueScores, spreadsByWeek[week], picksByUserWeek[(user.UserId, week)].ToList(),
                        CalculatorFor, user, week, now, seasonJuice.StartWeek);
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


    private async Task<LeaderboardWeekResults> CalculatePicks(List<NflScores> userScores, IEnumerable<NflSpreads> weekSpreads,
        List<NflPicks> userPicks, Func<int, Task<ISpreadCalculator>> calculatorFor, LeagueUserMapping user, int week,
        DateTimeOffset now, int startWeek) {
        var weekResult = new LeaderboardWeekResults {
            Week = week
        };

        // frizat-o3x: a week before the league's configured StartWeek needs no pick/spread
        // evaluation at all — it's not a win, loss, or pending state, just excluded from the
        // league's season entirely (see LeaderboardSettlementHelper for why this must never be
        // confused with MissingPicks/MissingGameResults).
        if (GameHelpers.IsWeekExcludedFromSeason(week, startWeek)) {
            weekResult.WeekResult = WeekResult.Excluded;
            return weekResult;
        }

        var spreadCalculator = await calculatorFor(week);
        var allPicksBeatSpread = userPicks.All(pick => {
            try {
                return IsPickAWinner(userScores, week, pick, spreadCalculator);
            } catch (Exception ex) {
                logger.LogError(ex, "Error calculating pick winner for user {User} week {Week} pick {@Pick}",
                    user.UserId, week, pick);
                return false;
            }
        });
        // A user can submit or change a pick for any individual game right up until that game's own
        // kickoff — so an incomplete pick set is only a genuine, terminal loss once every game for
        // this week has already started (e.g. Thursday Night Football already final doesn't mean
        // Sunday's picking window has closed too). Until then it's no different from "not decided
        // yet", so it reuses MissingGameResults (frizat-tf1: a real user was shown as losing a week
        // before any of that week's games had even kicked off — same root cause, CFB side).
        var allGamesStarted = GameHelpers.AllGamesStarted(weekSpreads.Select(s => s.GameTime), now);
        var incompletePicksResult = allGamesStarted ? WeekResult.MissingPicks : WeekResult.MissingGameResults;
        if (!allPicksBeatSpread) {
            weekResult.WeekResult = WeekResult.Lost; // Any loss is an immediate full week loss
        }
        else if (userPicks.Count < GameHelpers.GetRequiredPicks(week)) {
            logger.LogDebug("{User} {League} Missing Picks {Week} {Count} {Required}", user.User, user.League.LeagueName, week, userPicks.Count,
                GameHelpers.GetRequiredPicks(week));
            weekResult.WeekResult = incompletePicksResult;
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

        return LeaderboardSettlementHelper.SettleWeeks(leaderboard, leagueJuice.WeeklyCost, maxWeek, leagueJuice.StartWeek);
    }

}
