using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Shared.Helpers.Extensions;

public static class EspnScoresExtensions {
    public static bool IsPostSeason(this EspnScores scores) {
        if (scores.Season is null)
            return false;
        if (scores!.Season.Type == (int)TypeOfSeason.PostSeason)
            return true;
        return false;
    }
    public static bool IsPostSeason(this Event scoreEvent) {
        if (scoreEvent.Season is null)
            return false;
        if (scoreEvent!.Season.Type == (int)TypeOfSeason.PostSeason)
            return true;
        return false;
    }

    // frizat-4k9: one predicate for the Pro-Bowl-week-skip quirk (ESPN's raw week 5 meaning
    // "Super Bowl" through season <= GameHelpers.LastSeasonEspnSkippedProBowlWeek), instead of
    // ESPNApiService.FixEspnProbBowlWeek writing the same three-part condition out twice — once
    // for EspnScores, once per Event — the same duplication-across-two-ESPN-shapes problem
    // IsPostSeason above already solves via this file's overload pattern.
    public static bool NeedsProBowlWeekFix(this EspnScores scores) =>
        scores.IsPostSeason() && scores.Week.Number == 5 && scores.Season.Year <= GameHelpers.LastSeasonEspnSkippedProBowlWeek;
    public static bool NeedsProBowlWeekFix(this Event scoreEvent) =>
        scoreEvent.IsPostSeason() && scoreEvent.Week.Number == 5 && scoreEvent.Season.Year <= GameHelpers.LastSeasonEspnSkippedProBowlWeek;
}
