using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Enum;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

// Shared pairwise settlement, called by both LeaderboardService (NFL) and CfbLeaderboardService
// (CFB). Tested once here rather than duplicated per sport — see LeaderboardTests.cs for the
// thinner per-sport wiring checks.
public class LeaderboardSettlementHelperTests {
    private static LeaderboardModel MakeUser(string id, params WeekResult[] weekResults) =>
        new() {
            User = new ApplicationUser { Id = id },
            WeekResults = weekResults.Select((r, i) => new LeaderboardWeekResults { Week = i + 1, WeekResult = r }).ToArray(),
        };

    [Fact]
    public void SettleWeeks_DecidedMixed_PaysOutAsToday() {
        var leaderboard = new List<LeaderboardModel> {
            MakeUser("w1", WeekResult.Won),
            MakeUser("w2", WeekResult.Won),
            MakeUser("l1", WeekResult.Lost),
        };

        var result = LeaderboardSettlementHelper.SettleWeeks(leaderboard, baseWeeklyCost: 10, weekCount: 1);

        Assert.Equal(10, result.Single(u => u.User.Id == "w1").WeekResults[0].Score);
        Assert.Equal(10, result.Single(u => u.User.Id == "w2").WeekResults[0].Score);
        Assert.Equal(-20, result.Single(u => u.User.Id == "l1").WeekResults[0].Score);
    }

    [Fact]
    public void SettleWeeks_AllPush_DoublesPotForNextWeek() {
        var leaderboard = new List<LeaderboardModel> {
            MakeUser("a", WeekResult.Won, WeekResult.Won),
            MakeUser("b", WeekResult.Won, WeekResult.Lost),
        };

        var result = LeaderboardSettlementHelper.SettleWeeks(leaderboard, baseWeeklyCost: 10, weekCount: 2);

        // Week 1: all-won push -> everyone scores 0, pot doubles to 20 for week 2.
        Assert.Equal(0, result.Single(u => u.User.Id == "a").WeekResults[0].Score);
        Assert.Equal(0, result.Single(u => u.User.Id == "b").WeekResults[0].Score);
        // Week 2: mixed at the doubled $20 cost.
        Assert.Equal(20, result.Single(u => u.User.Id == "a").WeekResults[1].Score);
        Assert.Equal(-20, result.Single(u => u.User.Id == "b").WeekResults[1].Score);
    }

    [Fact]
    public void SettleWeeks_OnePendingAmongDecidedMixed_PaysDecidedUsersProvisionally_MarksWeekNotFinal() {
        var leaderboard = new List<LeaderboardModel> {
            MakeUser("w1", WeekResult.Won),
            MakeUser("l1", WeekResult.Lost),
            MakeUser("p1", WeekResult.MissingGameResults),
        };

        var result = LeaderboardSettlementHelper.SettleWeeks(leaderboard, baseWeeklyCost: 10, weekCount: 1);

        Assert.Equal(10, result.Single(u => u.User.Id == "w1").WeekResults[0].Score);
        Assert.Equal(-10, result.Single(u => u.User.Id == "l1").WeekResults[0].Score);
        Assert.Equal(0, result.Single(u => u.User.Id == "p1").WeekResults[0].Score);
    }

    [Fact]
    public void SettleWeeks_PendingWithNoDecidedLosersYet_NoPayoutYet_PotNotRolled() {
        var leaderboard = new List<LeaderboardModel> {
            MakeUser("w1", WeekResult.Won),
            MakeUser("p1", WeekResult.MissingGameResults),
        };

        var result = LeaderboardSettlementHelper.SettleWeeks(leaderboard, baseWeeklyCost: 10, weekCount: 1);

        Assert.Equal(0, result.Single(u => u.User.Id == "w1").WeekResults[0].Score);
        Assert.Equal(0, result.Single(u => u.User.Id == "p1").WeekResults[0].Score);
    }

    [Fact]
    public void SettleWeeks_MissingPicksStillBucketsAsLoser() {
        var leaderboard = new List<LeaderboardModel> {
            MakeUser("w1", WeekResult.Won),
            MakeUser("mp1", WeekResult.MissingPicks),
        };

        var result = LeaderboardSettlementHelper.SettleWeeks(leaderboard, baseWeeklyCost: 10, weekCount: 1);

        Assert.Equal(10, result.Single(u => u.User.Id == "w1").WeekResults[0].Score);
        Assert.Equal(-10, result.Single(u => u.User.Id == "mp1").WeekResults[0].Score);
    }

    // The invariant that makes showing provisional numbers safe: once the pending pick resolves,
    // the numbers must land exactly where a "nobody was ever pending" computation would have put
    // them — no permanent drift from having been shown provisionally first.
    [Fact]
    public void SettleWeeks_AfterPendingPickResolves_MatchesNeverPendingBaseline() {
        var round1 = new List<LeaderboardModel> {
            MakeUser("w1", WeekResult.Won),
            MakeUser("l1", WeekResult.Lost),
            MakeUser("p1", WeekResult.MissingGameResults),
        };
        LeaderboardSettlementHelper.SettleWeeks(round1, baseWeeklyCost: 10, weekCount: 1);

        var round2 = new List<LeaderboardModel> {
            MakeUser("w1", WeekResult.Won),
            MakeUser("l1", WeekResult.Lost),
            MakeUser("p1", WeekResult.Won),
        };
        var settledRound2 = LeaderboardSettlementHelper.SettleWeeks(round2, baseWeeklyCost: 10, weekCount: 1);

        var baseline = new List<LeaderboardModel> {
            MakeUser("w1", WeekResult.Won),
            MakeUser("l1", WeekResult.Lost),
            MakeUser("p1", WeekResult.Won),
        };
        var settledBaseline = LeaderboardSettlementHelper.SettleWeeks(baseline, baseWeeklyCost: 10, weekCount: 1);

        foreach (var id in new[] { "w1", "l1", "p1" }) {
            Assert.Equal(
                settledBaseline.Single(u => u.User.Id == id).WeekResults[0].Score,
                settledRound2.Single(u => u.User.Id == id).WeekResults[0].Score);
        }
    }

    // /code-review: a week with a real decided winner AND a real decided loser can never turn
    // out to be a push, no matter how a third, still-pending user resolves — adding one more
    // Won or Lost only ever grows an already-nonempty bucket. So the pot must reset to base for
    // the NEXT week the moment that's known, not wait for weekPending to clear on THIS week —
    // otherwise a pot inflated by an earlier push stays inflated forever once any later week
    // gets stuck pending, silently over/under-paying every week after it.
    [Fact]
    public void SettleWeeks_PotResetsForNextWeek_EvenWhileThisWeekIsStillPending_OnceWinnerAndLoserAreBothDecided() {
        var leaderboard = new List<LeaderboardModel> {
            // Week 1: all-push (a doubles the pot to $20 for week 2).
            MakeUser("a", WeekResult.Won, WeekResult.Won, WeekResult.Won),
            MakeUser("b", WeekResult.Won, WeekResult.Lost, WeekResult.Lost),
            // Week 2 needs a third user so it can have both a winner AND a loser decided
            // while still being "pending" overall (c is MissingGameResults in week 2 only).
            // c must also be a genuine winner in week 1 — anything else breaks the all-push
            // premise for week 1 (which is what needs to double the pot in the first place).
            MakeUser("c", WeekResult.Won, WeekResult.MissingGameResults, WeekResult.Won),
        };

        var result = LeaderboardSettlementHelper.SettleWeeks(leaderboard, baseWeeklyCost: 10, weekCount: 3);

        // Week 2: provisional settle at the inherited (doubled) $20 pot — a and b are decided,
        // c is genuinely pending. This part already worked before the fix.
        Assert.Equal(20, result.Single(u => u.User.Id == "a").WeekResults[1].Score);
        Assert.Equal(-20, result.Single(u => u.User.Id == "b").WeekResults[1].Score);

        // Week 3: fully decided (2 winners, 1 loser) — must use the reset $10 base, not the
        // stale $20 that should have already been retired the moment week 2 got its own
        // winner+loser (guaranteeing week 2 could never become a push).
        Assert.Equal(10, result.Single(u => u.User.Id == "a").WeekResults[2].Score);
        Assert.Equal(10, result.Single(u => u.User.Id == "c").WeekResults[2].Score);
        Assert.Equal(-20, result.Single(u => u.User.Id == "b").WeekResults[2].Score);
    }

    // frizat-o3x: a league that excludes its early week(s) via StartWeek has every member's
    // WeekResult for that index set to Excluded (LeaderboardService/CfbLeaderboardService's own
    // job, not this helper's) AND passes the same startWeek explicitly — SettleWeeks trusts the
    // parameter, not a scan of the cells, as the source of truth for which weeks to skip. Before
    // this fix, an all-Excluded week looked exactly like an all-MissingPicks week — zero winners,
    // zero losers, not "pending" — and fell into the SAME push branch as a genuine all-lost week,
    // doubling the pot for the league's real first week. That is exactly the bug this bead exists
    // to prevent ("week 2 would be a normal week").
    [Fact]
    public void SettleWeeks_ExcludedWeek_IsSkippedEntirely_NextRealWeekSettlesAtBaseCost() {
        var leaderboard = new List<LeaderboardModel> {
            // Week 1 (index 0) excluded for the whole league — nobody was ever asked to pick.
            MakeUser("w1", WeekResult.Excluded, WeekResult.Won),
            MakeUser("l1", WeekResult.Excluded, WeekResult.Lost),
        };

        var result = LeaderboardSettlementHelper.SettleWeeks(leaderboard, baseWeeklyCost: 10, weekCount: 2, startWeek: 2);

        // Excluded week: no score change, not treated as a push.
        Assert.Equal(0, result.Single(u => u.User.Id == "w1").WeekResults[0].Score);
        Assert.Equal(0, result.Single(u => u.User.Id == "l1").WeekResults[0].Score);
        // Week 2 (the league's real first week): settles at the untouched base cost, NOT doubled.
        Assert.Equal(10, result.Single(u => u.User.Id == "w1").WeekResults[1].Score);
        Assert.Equal(-10, result.Single(u => u.User.Id == "l1").WeekResults[1].Score);
    }

    [Fact]
    public void SettleWeeks_ExcludedWeek_DoesNotSuppressARealPushInTheFollowingWeek() {
        var leaderboard = new List<LeaderboardModel> {
            // Week 1 excluded; week 2 is a genuine all-won push; week 3 is decided mixed.
            MakeUser("a", WeekResult.Excluded, WeekResult.Won, WeekResult.Won),
            MakeUser("b", WeekResult.Excluded, WeekResult.Won, WeekResult.Lost),
        };

        var result = LeaderboardSettlementHelper.SettleWeeks(leaderboard, baseWeeklyCost: 10, weekCount: 3, startWeek: 2);

        // Week 2: still a real push — everyone scores 0, pot doubles for week 3.
        Assert.Equal(0, result.Single(u => u.User.Id == "a").WeekResults[1].Score);
        Assert.Equal(0, result.Single(u => u.User.Id == "b").WeekResults[1].Score);
        // Week 3: settles at the doubled $20, proving the excluded week didn't reset/suppress it.
        Assert.Equal(20, result.Single(u => u.User.Id == "a").WeekResults[2].Score);
        Assert.Equal(-20, result.Single(u => u.User.Id == "b").WeekResults[2].Score);
    }
}
