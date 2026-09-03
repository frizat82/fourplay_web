using FourPlayWebApp.Server.Jobs;
using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Server.UnitTests;

public class CfbSlateHelpersTests {
    private static Competitor MakeCompetitor(int? curatedRank) => new() {
        HomeAway = HomeAway.Home, Score = 0, Team = new EspnTeam { Abbreviation = "X" }, Records = [],
        CuratedRank = curatedRank is { } r ? new CuratedRankInfo { Current = r } : null,
    };

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(25)]
    public void RankOf_RankedTeam_ReturnsRank(int rank) {
        Assert.Equal(rank, CfbSlateHelpers.RankOf(MakeCompetitor(rank)));
    }

    [Fact]
    public void RankOf_UnrankedSentinel99_ReturnsNull() {
        Assert.Null(CfbSlateHelpers.RankOf(MakeCompetitor(99)));
    }

    [Fact]
    public void RankOf_NoCuratedRank_ReturnsNull() {
        Assert.Null(CfbSlateHelpers.RankOf(MakeCompetitor(null)));
    }

    // Tuesday 7:00pm ET = 23:00 UTC (EDT, UTC-4) — a typical MACtion weeknight kickoff.
    [Fact]
    public void IsMidweekGame_TuesdayEt_ReturnsTrue() {
        var tuesdayEt = new DateTimeOffset(2025, 9, 30, 23, 0, 0, TimeSpan.Zero); // Tue Sep 30 7pm ET
        Assert.True(CfbSlateHelpers.IsMidweekGame(tuesdayEt));
    }

    [Fact]
    public void IsMidweekGame_WednesdayEt_ReturnsTrue() {
        var wednesdayEt = new DateTimeOffset(2025, 10, 1, 23, 0, 0, TimeSpan.Zero); // Wed Oct 1 7pm ET
        Assert.True(CfbSlateHelpers.IsMidweekGame(wednesdayEt));
    }

    [Fact]
    public void IsMidweekGame_Saturday_ReturnsFalse() {
        var saturdayEt = new DateTimeOffset(2025, 9, 27, 19, 0, 0, TimeSpan.Zero); // Sat Sep 27 3pm ET
        Assert.False(CfbSlateHelpers.IsMidweekGame(saturdayEt));
    }

    [Fact]
    public void IsMidweekGame_ThursdayEt_ReturnsFalse() {
        var thursdayEt = new DateTimeOffset(2025, 9, 25, 23, 0, 0, TimeSpan.Zero); // Thu Sep 25 7pm ET
        Assert.False(CfbSlateHelpers.IsMidweekGame(thursdayEt));
    }

    // UTC-Wednesday-early-morning that is actually Tuesday night ET — must convert before checking.
    [Fact]
    public void IsMidweekGame_UtcWednesdayEarlyMorning_IsActuallyTuesdayEt_ReturnsTrue() {
        // Tue Sep 30 11:00pm ET = Wed Oct 1 03:00 UTC (EDT, UTC-4)
        var lateNight = new DateTimeOffset(2025, 10, 1, 3, 0, 0, TimeSpan.Zero);
        Assert.True(CfbSlateHelpers.IsMidweekGame(lateNight));
    }
}
