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
            var scoresByWeek = leagueScores.ToLookup(s => s.NflWeek);
            // One calculator per week, shared by every member (each build costs cache lookups, and
            // the answer doesn't depend on who's asking). One DI scope for the whole run.
            await using var scope = scopeFactory.CreateAsyncScope();
            var spreadCalculatorProvider = scope.ServiceProvider.GetRequiredService<ISpreadCalculatorProvider>();
            var calculators = new Dictionary<int, ISpreadCalculator>();
            async Task<ISpreadCalculator> CalculatorFor(int week) {
                if (!calculators.TryGetValue(week, out var calc)) {
                    calc = await spreadCalculatorProvider.GetForNflWeekAsync(leagueId, (int)seasonYear, week);
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
                    var weekResult = await CalculatePicks(scoresByWeek[week].ToList<IScoreRow>(), spreadsByWeek[week], picksByUserWeek[(user.UserId, week)].ToList(),
                        CalculatorFor, week, now, seasonJuice.StartWeek);
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


    private async Task<LeaderboardWeekResults> CalculatePicks(IReadOnlyCollection<IScoreRow> weekScores, IEnumerable<NflSpreads> weekSpreads,
        List<NflPicks> userPicks, Func<int, Task<ISpreadCalculator>> calculatorFor, int week,
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
        weekResult.WeekResult = WeekOutcome.Evaluate(
            userPicks.Select(p => new PickRow(p.Team, p.Pick)).ToList(),
            weekScores,
            spreadCalculator,
            GameHelpers.GetRequiredPicks(week),
            GameHelpers.AllGamesStarted(weekSpreads.Select(s => s.GameTime), now));
        return weekResult;
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
