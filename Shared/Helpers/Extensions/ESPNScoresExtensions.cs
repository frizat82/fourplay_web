using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Shared.Helpers.Extensions;

public static class EspnScoresExtensions {
    // ESPN's own season type on an event — read only at the ESPN boundary (contract tests). Our
    // weeks' postseason flag is the control table's IsPostSeason (frizat-d47).
    public static bool IsPostSeason(this Event scoreEvent) =>
        scoreEvent.Season?.Type == (int)TypeOfSeason.PostSeason;
}
