using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Enum;

namespace FourPlayWebApp.Server.Services;

// Shared by CfbLeaderboardService.CalculateTotals and LeaderboardService.CalculateUserTotals
// (CLAUDE.md: NFL/CFB business logic lives in one shared function, never two copies).
public static class LeaderboardSettlementHelper {
    // Whether a week is not yet fully decided — a week is "not final" iff any league member's own
    // pick for it is still MissingGameResults. The frontend derives the identical "Not Final"
    // signal itself from the same WeekResult field it already receives (no separate flag needed
    // on the wire), so this only needs to be public for use inside SettleWeeks below.
    private static bool IsWeekPending(List<LeaderboardModel> leaderboard, int weekIndex) =>
        leaderboard.Any(u => u.WeekResults[weekIndex].WeekResult == WeekResult.MissingGameResults);

    // Pairwise settlement — every loser owes every winner one weekly cost. Runs every week even
    // while pending: users whose own pick is already decided (Won/Lost/MissingPicks) settle
    // provisionally against each other now; a user still MissingGameResults for that week is
    // excluded from both buckets and their Score stays untouched until their own pick resolves.
    // The push-doubling pot roll only ever fires once a week is genuinely fully decided — a
    // provisional split must never trigger, or skip, it.
    public static List<LeaderboardModel> SettleWeeks(List<LeaderboardModel> leaderboard, long baseWeeklyCost, int weekCount) {
        var currentWeeklyCost = baseWeeklyCost;

        for (int i = 0; i < weekCount; i++) {
            var weekPending = IsWeekPending(leaderboard, i);
            var winners = new List<LeaderboardWeekResults>();
            var losers = new List<LeaderboardWeekResults>();

            foreach (var user in leaderboard) {
                var cell = user.WeekResults[i];
                switch (cell.WeekResult) {
                    case WeekResult.Won: winners.Add(cell); break;
                    case WeekResult.MissingGameResults: break; // genuinely pending, no bucket yet
                    default: losers.Add(cell); break; // Lost or MissingPicks — already a terminal loss
                }
            }

            if (winners.Count > 0 && losers.Count > 0) {
                foreach (var cell in winners) cell.Score = losers.Count * currentWeeklyCost;
                foreach (var cell in losers) cell.Score = -(winners.Count * currentWeeklyCost);
                // Once a week has a real decided winner AND a real decided loser, it can never
                // turn out to be a push — one more decided pick (from anyone still pending) only
                // ever grows an already-nonempty bucket. So the pot resets for the NEXT week the
                // moment that's known, regardless of whether THIS week's own pending user has
                // resolved yet (/code-review: an earlier push's inflated pot was otherwise stuck
                // carrying forward through every later week until the stuck pending user cleared).
                currentWeeklyCost = baseWeeklyCost;
            } else if (!weekPending) {
                // Fully decided and a push (all-won or all-lost) — roll the pot as today.
                currentWeeklyCost += baseWeeklyCost;
                foreach (var user in leaderboard) user.WeekResults[i].Score = 0;
            }
            // else: still pending and not enough decided info yet to know a winner/loser split —
            // leave Scores and currentWeeklyCost untouched; resolves on a later, fuller load.
        }

        foreach (var user in leaderboard) user.Total = user.WeekResults.Sum(w => w.Score);
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
