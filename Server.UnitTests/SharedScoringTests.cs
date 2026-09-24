using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Enum;

namespace FourPlayWebApp.Server.UnitTests;

// One implementation for both sports (CLAUDE.md): which tease a period uses (JuiceTiers) and how a
// user's week/slate resolves (WeekOutcome). Previously NFL's SpreadCalculator.ResolveNflJuice /
// LeaderboardService.CalculatePicks and CFB's CfbLeaderboardService.JuiceForSlate / EvaluateSlate
// were two hand-written copies of the same rules.
public class SharedScoringTests {
    private static readonly LeagueJuiceMapping Mapping = new() { Juice = 13, JuiceDivisional = 10, JuiceConference = 6 };

    // ── JuiceTiers ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1, 13)]
    [InlineData(18, 13)]   // last regular-season week
    [InlineData(19, 10)]   // Wild Card
    [InlineData(20, 10)]   // Divisional
    [InlineData(21, 6)]    // Conference Championship
    [InlineData(22, 0)]    // Super Bowl — current rule: no tease (pending product decision)
    public void JuiceTiers_Nfl_ByWeek(int week, double expected) =>
        Assert.Equal(expected, JuiceTiers.For(LeagueType.Nfl, week, Mapping));

    [Theory]
    [InlineData(1, 13)]
    [InlineData(14, 13)]   // conference championships — still regular tease
    [InlineData(15, 10)]   // CFP first round
    [InlineData(16, 10)]   // quarterfinals
    [InlineData(17, 6)]    // semifinals — frizat-dcz: was <= 17 => JuiceDivisional (10), not 6
    [InlineData(18, 6)]    // championship
    [InlineData(19, 6)]
    public void JuiceTiers_Cfb_BySlate(int slate, double expected) =>
        Assert.Equal(expected, JuiceTiers.For(LeagueType.Cfb, slate, Mapping));

    // ── WeekOutcome ────────────────────────────────────────────────────────────

    private static readonly DateTimeOffset Now = new(2026, 9, 20, 18, 0, 0, TimeSpan.Zero);

    // KC -3 at home, no tease: KC 27-20 covers, BAL doesn't.
    private static SpreadCalculator Calc() => new(
        [new NflSpreads { HomeTeam = "KC", AwayTeam = "BAL", HomeTeamSpread = -3, AwayTeamSpread = 3, OverUnder = 45 }], juice: 0);
    private static readonly IScoreRow[] KcWins = [new NflScores { HomeTeam = "KC", AwayTeam = "BAL", HomeTeamScore = 27, AwayTeamScore = 20 }];

    [Fact]
    public void WeekOutcome_Won_WhenEveryRequiredPickCovers() =>
        Assert.Equal(WeekResult.Won, WeekOutcome.Evaluate([new PickRow("KC", PickType.Spread)], KcWins, Calc(), requiredPicks: 1, allGamesStarted: true));

    [Fact]
    public void WeekOutcome_Lost_AsSoonAsAnyPickLoses_EvenWithPicksMissing() =>
        Assert.Equal(WeekResult.Lost, WeekOutcome.Evaluate([new PickRow("BAL", PickType.Spread)], KcWins, Calc(), requiredPicks: 4, allGamesStarted: false));

    [Fact]
    public void WeekOutcome_IncompletePicks_AreMissingGameResults_UntilEveryGameHasStarted() {
        Assert.Equal(WeekResult.MissingGameResults, WeekOutcome.Evaluate([], KcWins, Calc(), requiredPicks: 4, allGamesStarted: false));
        Assert.Equal(WeekResult.MissingPicks, WeekOutcome.Evaluate([], KcWins, Calc(), requiredPicks: 4, allGamesStarted: true));
    }

    [Fact]
    public void WeekOutcome_PendingGame_IsMissingGameResults_NotAWin() =>
        Assert.Equal(WeekResult.MissingGameResults, WeekOutcome.Evaluate([new PickRow("SF", PickType.Spread)], KcWins, Calc(), requiredPicks: 1, allGamesStarted: true));

    // frizat-xtn: a pick with no matching spread row (bad data, a spread deleted/renamed after the
    // pick, an ESPN cache gap) is a LOSS, never a silent win — it was never evaluated against anything.
    [Fact]
    public void WeekOutcome_PickWithNoSpreadRow_FailsClosed() {
        IScoreRow[] scores = [new CfbScores { HomeTeam = "OSU", AwayTeam = "MICH", HomeTeamScore = 30, AwayTeamScore = 10 }];
        Assert.Equal(WeekResult.Lost, WeekOutcome.Evaluate([new PickRow("OSU", PickType.Spread)], scores, Calc(), requiredPicks: 1, allGamesStarted: true));
    }

    [Fact]
    public void WeekOutcome_OverUnder_UsesTheCombinedScore() {
        // 27 + 20 = 47 > 45
        Assert.Equal(WeekResult.Won, WeekOutcome.Evaluate([new PickRow("KC", PickType.Over)], KcWins, Calc(), requiredPicks: 1, allGamesStarted: true));
        Assert.Equal(WeekResult.Lost, WeekOutcome.Evaluate([new PickRow("KC", PickType.Under)], KcWins, Calc(), requiredPicks: 1, allGamesStarted: true));
    }
}
