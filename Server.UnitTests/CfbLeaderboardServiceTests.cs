using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Enum;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// frizat-dcz: JuiceForSlate maps slate number to the correct tease amount.
/// Slate 17 = CFB Semifinals → JuiceConference (6), NOT JuiceDivisional (10).
/// </summary>
public class CfbLeaderboardServiceTests
{
    private static LeagueJuiceMapping Juice() => new()
    {
        Juice = 13,          // regular season
        JuiceDivisional = 10, // quarterfinals (slates 15–16)
        JuiceConference = 6,  // semifinals (slate 17)
        WeeklyCost = 5,
    };

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(14)]
    public void JuiceForSlate_RegularSlates_ReturnsJuice(int slateNumber)
    {
        var result = CfbLeaderboardService.JuiceForSlate(slateNumber, Juice());
        Assert.Equal(13, result);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(16)]
    public void JuiceForSlate_QuarterfinalsSlates_ReturnsJuiceDivisional(int slateNumber)
    {
        var result = CfbLeaderboardService.JuiceForSlate(slateNumber, Juice());
        Assert.Equal(10, result);
    }

    [Fact]
    public void JuiceForSlate_Slate17_Semifinals_ReturnsJuiceConference()
    {
        // Bug: was <= 17 => JuiceDivisional, so Slate 17 returned 10 instead of 6.
        var result = CfbLeaderboardService.JuiceForSlate(17, Juice());
        Assert.Equal(6, result);
    }

    [Fact]
    public void JuiceForSlate_Slate18_Championship_ReturnsJuiceConference()
    {
        var result = CfbLeaderboardService.JuiceForSlate(18, Juice());
        Assert.Equal(6, result);
    }

    // frizat-xtn: a pick with no matching spread row (bad data, a spread deleted/renamed after
    // the pick was made, or an ESPN cache gap at pick time) must be a LOSS, not a silent WIN — the
    // pick was never actually evaluated against anything. Matches NFL's LeaderboardService, which
    // has no fail-open guard and relies on the shared SpreadCalculator.DidUserWinSpread's own
    // `if (spread is null) return false`.
    [Fact]
    public void DidPickWin_NoMatchingSpreadRow_ReturnsFalse()
    {
        var pick = new CfbPicks { Team = "GHOST", PickType = PickType.Spread };
        var scores = new List<CfbScores> { new() { HomeTeam = "GHOST", AwayTeam = "OPP", HomeTeamScore = 30, AwayTeamScore = 10 } };
        var spreads = new List<CfbSpreads>(); // no matching row for GHOST at all
        var spreadCalculator = new SpreadCalculator(spreads, juice: 0);

        var result = CfbLeaderboardService.DidPickWin(pick, spreads, scores, spreadCalculator);

        Assert.False(result);
    }
}
