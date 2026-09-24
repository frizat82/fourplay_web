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
            // avoids re-filtering the full-season lists for every one of the users × weeks below.
            var now = timeProvider.GetUtcNow();
            var spreadsByWeek = leagueSpreads.ToLookup(s => s.NflWeek);
            var scoresByWeek = leagueScores.ToLookup(s => s.NflWeek);
            // Each week's inputs are the same for every member, so they're built once per week —
            // calculator straight from the spreads and juice loaded above (exactly how the CFB
            // leaderboard builds its per-slate calculator), not a second, cached read of the same
            // spreads that could disagree with the fresh ones used for AllGamesStarted.
            var weeks = Enumerable.Range(1, maxWeek)
                .Where(week => !GameHelpers.IsWeekExcludedFromSeason(week, seasonJuice.StartWeek))
                .ToDictionary(week => week, week => new WeekInputs(
                    scoresByWeek[week].ToList<IScoreRow>(),
                    new SpreadCalculator(spreadsByWeek[week], JuiceTiers.For(LeagueType.Nfl, week, seasonJuice)),
                    GameHelpers.AllGamesStarted(spreadsByWeek[week].Select(s => s.GameTime), now)));

            foreach (var user in leagueUsers) {
                var userPoints = new LeaderboardModel {
                    WeekResults = new LeaderboardWeekResults[maxWeek],
                    User = user.User
                };
                for (int week = 1; week <= maxWeek; week++) {
                    userPoints.WeekResults[week - 1] = new LeaderboardWeekResults {
                        Week = week,
                        // frizat-o3x: a week before the league's StartWeek is excluded outright —
                        // not a win, loss or pending state (see LeaderboardSettlementHelper).
                        WeekResult = weeks.TryGetValue(week, out var inputs)
                            ? EvaluateWeek(inputs, picksByUserWeek[(user.UserId, week)], week)
                            : WeekResult.Excluded,
                    };
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


    private sealed record WeekInputs(List<IScoreRow> Scores, SpreadCalculator Calculator, bool AllGamesStarted);

    // How the week resolves is WeekOutcome — the same rules the CFB leaderboard uses.
    private WeekResult EvaluateWeek(WeekInputs inputs, IEnumerable<NflPicks> userPicks, int week) =>
        WeekOutcome.Evaluate(
            userPicks.Select(p => new PickRow(p.Team, p.Pick)).ToList(),
            inputs.Scores,
            inputs.Calculator,
            GameHelpers.GetRequiredPicks(week),
            inputs.AllGamesStarted,
            (pick, ex) => logger.LogError(ex, "Error scoring pick {@Pick} week {Week}", pick, week));

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
