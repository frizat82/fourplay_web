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
}
