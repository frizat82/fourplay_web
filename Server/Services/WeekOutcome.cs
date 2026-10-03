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
    /// <summary>
    /// A week's/slate's result plus which picks (if any) already lost — e.g. a "you lost the week"
    /// push notification can name the losing pick straight off this, without re-deriving the same
    /// win/loss arithmetic itself. LosingPicks is empty unless WeekResult is Lost.
    /// </summary>
    /// <param name="HadScoringError">
    /// True if any pick's scoring threw (bad data) rather than the pick legitimately losing against
    /// the spread. A page render self-corrects once the data is fixed, but a push notification
    /// doesn't get a second chance — a caller that permanently records "notified" should skip doing
    /// so here, so the week gets re-evaluated (and the right push sent) once the bug is fixed.
    /// </param>
    public readonly record struct Evaluation(WeekResult WeekResult, IReadOnlyList<PickRow> LosingPicks, bool HadScoringError);

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
    public static Evaluation Evaluate(IReadOnlyCollection<PickRow> picks, IReadOnlyCollection<IScoreRow> scores,
        ISpreadCalculator calculator, int requiredPicks, bool allGamesStarted,
        Action<PickRow, Exception>? onPickError = null) {
        var hadScoringError = false;
        var results = picks.Select(p => {
            try {
                return (pick: p, result: PickResult(p, scores, calculator));
            } catch (Exception ex) {
                hadScoringError = true;
                onPickError?.Invoke(p, ex);
                return (pick: p, result: (bool?)false);
            }
        }).ToList();
        var losingPicks = results.Where(r => r.result == false).Select(r => r.pick).ToList();
        if (losingPicks.Count > 0)
            return new Evaluation(WeekResult.Lost, losingPicks, hadScoringError); // any loss decides the week
        if (picks.Count < requiredPicks)
            return new Evaluation(allGamesStarted ? WeekResult.MissingPicks : WeekResult.MissingGameResults, [], false);
        if (results.Any(r => r.result is null)) return new Evaluation(WeekResult.MissingGameResults, [], false);
        return new Evaluation(WeekResult.Won, [], false);
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
