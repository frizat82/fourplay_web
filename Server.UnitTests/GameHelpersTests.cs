using FourPlayWebApp.Shared.Helpers;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

public class GameHelpersTests
{
    // Pick counts per round are no longer a GameHelpers rule: they're each control-table row's
    // RequiredPicks (frizat-d47), asserted against /pick-rules by PeriodRulesBackfillPostgresTests.

    // ─── GetTeamLogo ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("BUF", "Icons/Teams/buf.png")]
    [InlineData("DAL", "Icons/Teams/dal.png")]
    [InlineData("SEA", "Icons/Teams/sea.png")]
    public void GetTeamLogo_ReturnsCorrectPath(string abbreviation, string expected)
    {
        Assert.Equal(expected, GameHelpers.GetTeamLogo(abbreviation));
    }

    // ─── AllGamesStarted ────────────────────────────────────────────────────────
    // frizat-8y6: reported live — a CFB slate with zero spreads released yet (the picking window
    // hasn't even opened) was shown on the leaderboard as a terminal loss (MissingPicks) for
    // every user, because LINQ's .All() on an empty sequence is vacuously true — "all games
    // started" read as true for a week with NO games to check, the opposite of the real answer.
    // Both LeaderboardService.CalculatePicks (NFL) and CfbLeaderboardService.EvaluateSlate (CFB)
    // key their MissingPicks-vs-MissingGameResults decision directly off this return value.

    [Fact]
    public void AllGamesStarted_ReturnsFalse_WhenGameTimesIsEmpty() {
        // No games exist for this week/slate at all yet — cannot conclude they've "all started".
        Assert.False(GameHelpers.AllGamesStarted([], DateTimeOffset.UtcNow));
    }

    [Fact]
    public void AllGamesStarted_ReturnsTrue_WhenEveryGameTimeHasPassed() {
        var now = DateTimeOffset.UtcNow;
        var gameTimes = new[] { now.AddHours(-2), now.AddHours(-1) };
        Assert.True(GameHelpers.AllGamesStarted(gameTimes, now));
    }

    [Fact]
    public void AllGamesStarted_ReturnsFalse_WhenAnyGameTimeIsInTheFuture() {
        var now = DateTimeOffset.UtcNow;
        var gameTimes = new[] { now.AddHours(-2), now.AddHours(1) };
        Assert.False(GameHelpers.AllGamesStarted(gameTimes, now));
    }

    // Period labels are each control-table row's own WeekLabel (frizat-d47) — no GameHelpers rule.
}
