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

    // ─── GetEspnRequiredPicks ─────────────────────────────────────────────────

    [Theory]
    [InlineData(1, false, 4)]
    [InlineData(9, false, 4)]
    [InlineData(18, false, 4)]
    [InlineData(1, true, 3)]   // Wild Card (6 games, pick 3)
    [InlineData(2, true, 3)]   // Divisional (4 games, pick 3)
    [InlineData(3, true, 2)]   // Conference Championship (2 games, pick 2)
    [InlineData(4, true, 1)]   // Super Bowl (stored at NflWeek 22 via wk5→4 hack in NflScoresJob)
    [InlineData(5, true, 1)]   // Super Bowl (ESPN week 5)
    public void GetEspnRequiredPicks_ReturnsCorrectCount(long week, bool isPostSeason, int expected)
    {
        Assert.Equal(expected, GameHelpers.GetEspnRequiredPicks(week, isPostSeason));
    }

    [Fact]
    public void GetEspnRequiredPicks_InvalidPostSeasonWeek_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => GameHelpers.GetEspnRequiredPicks(6, true));
    }

    // ─── GetRequiredPicks ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(1, 4)]
    [InlineData(18, 4)]
    [InlineData(19, 3)]  // Wild Card (6 games, pick 3)
    [InlineData(20, 3)]  // Divisional (4 games, pick 3)
    [InlineData(21, 2)]  // Conference Championship (2 games, pick 2)
    [InlineData(22, 1)]  // Super Bowl (stored at NflWeek 22 via wk5→4 hack in NflScoresJob)
    public void GetRequiredPicks_ReturnsCorrectCount(long week, int expected)
    {
        Assert.Equal(expected, GameHelpers.GetRequiredPicks(week));
    }

    [Fact]
    public void GetRequiredPicks_InvalidPostSeasonWeek_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => GameHelpers.GetRequiredPicks(23));
    }

    // ─── GetCfbRequiredPicks ──────────────────────────────────────────────────
    // 18-slate system: 1-14=Standard(4), 15-16=NFLDivisional(3), 17=NFLConference(2), 18=NFLSuperBowl(1)

    [Theory]
    [InlineData(1,  4)]
    [InlineData(7,  4)]
    [InlineData(13, 4)]  // last regular-season week
    [InlineData(14, 4)]  // Conf. Championships (Standard format — same pick count as regular season)
    [InlineData(15, 3)]  // CFP First Round (NFLDivisional)
    [InlineData(16, 3)]  // CFP Quarterfinals (NFLDivisional)
    [InlineData(17, 2)]  // CFP Semifinals (NFLConference)
    [InlineData(18, 1)]  // CFP Championship (NFLSuperBowl)
    public void GetCfbRequiredPicks_ReturnsCorrectCount(int slateNumber, int expected)
    {
        Assert.Equal(expected, GameHelpers.GetCfbRequiredPicks(slateNumber));
    }

    // ─── GetTeamLogo ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("BUF", "Icons/Teams/buf.png")]
    [InlineData("DAL", "Icons/Teams/dal.png")]
    [InlineData("SEA", "Icons/Teams/sea.png")]
    public void GetTeamLogo_ReturnsCorrectPath(string abbreviation, string expected)
    {
        Assert.Equal(expected, GameHelpers.GetTeamLogo(abbreviation));
    }

    // ─── DaysHoursMinutesUntilNoonCst ─────────────────────────────────────────

    [Fact]
    public void DaysHoursMinutesUntilNoonCst_ReturnNullOrString()
    {
        // Just verify it doesn't throw — it depends on the current time
        var result = GameHelpers.DaysHoursMinutesUntilNoonCst();
        // Can be null (if already past Sunday noon) or a string like "2d 5h 30m"
        if (result is not null)
        {
            Assert.Matches(@"\d+d \d+h \d+m", result);
        }
    }
}
