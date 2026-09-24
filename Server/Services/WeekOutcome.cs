using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Enum;

namespace FourPlayWebApp.Server.Services;

/// <summary>A pick as scoring sees it — NflPicks and CfbPicks both map to this.</summary>
public sealed record PickRow(string Team, PickType PickType);

/// <summary>
/// How one user's week (NFL) or slate (CFB) resolves — one implementation for both leaderboards.
/// </summary>
public static class WeekOutcome {
    /// <param name="scores">This week's/slate's scores only.</param>
    /// <param name="allGamesStarted">
    /// A pick can be made or changed until its own game kicks off, so an incomplete pick set is only
    /// a terminal MissingPicks once every game has started — until then it's MissingGameResults
    /// (frizat-tf1: a user was shown losing a week before any game had kicked off).
    /// </param>
    /// <param name="onPickError">
    /// A pick that fails to score (bad data) counts as a loss and is reported here — one bad row must
    /// never take down the whole leaderboard build (both sports' scorers always isolated this).
    /// </param>
    public static WeekResult Evaluate(IReadOnlyCollection<PickRow> picks, IReadOnlyCollection<IScoreRow> scores,
        ISpreadCalculator calculator, int requiredPicks, bool allGamesStarted,
        Action<PickRow, Exception>? onPickError = null) {
        var results = picks.Select(p => {
            try {
                return PickResult(p, scores, calculator);
            } catch (Exception ex) {
                onPickError?.Invoke(p, ex);
                return false;
            }
        }).ToList();
        if (results.Any(r => r == false)) return WeekResult.Lost; // any loss decides the week
        if (picks.Count < requiredPicks)
            return allGamesStarted ? WeekResult.MissingPicks : WeekResult.MissingGameResults;
        if (results.Any(r => r is null)) return WeekResult.MissingGameResults;
        return WeekResult.Won;
    }

    /// <summary>true/false once the pick's game has a score; null while it doesn't.</summary>
    private static bool? PickResult(PickRow pick, IEnumerable<IScoreRow> scores, ISpreadCalculator calculator) {
        var score = scores.FirstOrDefault(s => s.HomeTeam == pick.Team || s.AwayTeam == pick.Team);
        if (score is null) return null;
        var isHome = score.HomeTeam == pick.Team;
        return calculator.DidUserWinPick(pick.Team,
            isHome ? score.HomeTeamScore : score.AwayTeamScore,
            isHome ? score.AwayTeamScore : score.HomeTeamScore,
            pick.PickType);
    }
}
