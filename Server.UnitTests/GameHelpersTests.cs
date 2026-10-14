using FourPlayWebApp.Shared.Helpers;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

public class GameHelpersTests
{
    // ─── GetWeekFromEspnWeek ───────────────────────────────────────────────────

    [Theory]
    [InlineData(1, 2025, false, 1)]
    [InlineData(5, 2025, false, 5)]
    [InlineData(18, 2025, false, 18)]
    [InlineData(1, 2025, true, 19)]
    [InlineData(2, 2025, true, 20)]
    [InlineData(3, 2025, true, 21)]
    [InlineData(4, 2025, true, 22)]
    // frizat: through the 2025 season, 5 was ESPN's own raw Super Bowl week number (it skipped
    // week 4 = Pro Bowl) — some callers (NFLScoresJob) used to pre-adjust 5->4 before calling
    // this; that's no longer necessary now that the season gate below handles it directly.
    [InlineData(5, 2025, true, 22)]
    // frizat: the NFL discontinued the Pro Bowl GAME starting with the 2026 season (announced
    // 2026-08-26, see docs/ESPNProBowl.md) — no gap expected in ESPN's postseason numbering from
    // here on, so week=4 (not 5) is expected to be the Super Bowl going forward. TODO: verify
    // against a real ESPN response once the 2026 postseason bracket actually exists (~Jan 2027).
    [InlineData(1, 2026, true, 19)]
    [InlineData(2, 2026, true, 20)]
    [InlineData(3, 2026, true, 21)]
    [InlineData(4, 2026, true, 22)]
    // A raw week=5 postseason response for 2026+ is NOT remapped to 22 — it's expected to be
    // unreachable under the "gap closed" assumption, and if the assumption is wrong this should
    // fail loudly (a nonexistent WeekId 23) rather than silently coincide with the right answer.
    [InlineData(5, 2026, true, 23)]
    public void GetWeekFromEspnWeek_ReturnsCorrectWeek(int espnWeek, int season, bool isPostSeason, int expected)
    {
        Assert.Equal(expected, GameHelpers.GetWeekFromEspnWeek(espnWeek, season, isPostSeason));
    }

    // ─── GetWeekName ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1, false, "Week 1")]
    [InlineData(7, false, "Week 7")]
    [InlineData(18, false, "Week 18")]
    [InlineData(1, true, "Wild Card")]
    [InlineData(2, true, "Divisional Round")]
    [InlineData(3, true, "Conference Championship")]
    [InlineData(4, true, "Super Bowl")]
    public void GetWeekName_ReturnsCorrectName(long week, bool isPostSeason, string expected)
    {
        Assert.Equal(expected, GameHelpers.GetWeekName(week, isPostSeason));
    }

    [Fact]
    public void GetWeekName_InvalidPostSeasonWeek_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => GameHelpers.GetWeekName(5, true));
    }

    // ─── GetWeekFromName ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("Week 1", false, 1)]
    [InlineData("Week 12", false, 12)]
    [InlineData("Wild Card", true, 1)]
    [InlineData("Divisional Round", true, 2)]
    [InlineData("Conference Championship", true, 3)]
    [InlineData("Super Bowl", true, 4)]
    public void GetWeekFromName_ReturnsCorrectWeek(string weekName, bool isPostSeason, int expected)
    {
        Assert.Equal(expected, GameHelpers.GetWeekFromName(weekName, isPostSeason));
    }

    [Fact]
    public void GetWeekFromName_InvalidPostSeasonName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => GameHelpers.GetWeekFromName("Pro Bowl", true));
    }

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

    // ─── Period labels (match what the app shows, so notifications read the same) ───

    [Theory]
    [InlineData(1, "Week 1")]
    [InlineData(18, "Week 18")]
    [InlineData(19, "Wild Card")]
    [InlineData(20, "Divisional Round")]
    [InlineData(21, "Conference Championship")]
    [InlineData(22, "Super Bowl")]
    public void GetNflWeekLabel_MatchesAppLabels(int nflWeek, string expected) =>
        Assert.Equal(expected, GameHelpers.GetNflWeekLabel(nflWeek));

    [Theory]
    [InlineData(1, "Week 1")]
    [InlineData(5, "Week 5")]
    [InlineData(13, "Week 13")]
    [InlineData(14, "Conf. Championships")]
    [InlineData(15, "CFP First Round")]
    [InlineData(16, "CFP Quarterfinals")]
    [InlineData(17, "CFP Semifinals")]
    [InlineData(18, "CFP Championship")]
    public void GetCfbSlateLabel_MatchesAppLabels(int slateNumber, string expected) =>
        Assert.Equal(expected, GameHelpers.GetCfbSlateLabel(slateNumber));
}
