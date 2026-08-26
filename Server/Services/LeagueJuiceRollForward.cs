using FourPlayWebApp.Server.Models.Data;

namespace FourPlayWebApp.Server.Services;

// frizat-ugs: shared "carry forward Juice from prior season" logic — used by both the manual
// RollForwardJuice endpoint (LeagueController, which requires an explicit prior season and errors
// otherwise) and the automatic LeagueJuiceLockJob (which falls back to entity defaults when no
// prior season exists). Pure functions, no I/O — easy to unit test directly, no service/DI needed.
public static class LeagueJuiceRollForward {
    public static LeagueJuiceMapping? FindPriorSeasonMapping(int toSeason, IEnumerable<LeagueJuiceMapping> allMappingsForLeague) =>
        allMappingsForLeague.Where(m => m.Season < toSeason).MaxBy(m => m.Season);

    // copyFrom's four values are copied verbatim when given; otherwise the entity's own property
    // defaults (Juice=13 etc.) supply the fallback — never re-hardcode those numbers here.
    public static LeagueJuiceMapping BuildMapping(int leagueId, int toSeason, LeagueJuiceMapping? copyFrom) {
        var mapping = new LeagueJuiceMapping { LeagueId = leagueId, Season = toSeason, DateCreated = DateTimeOffset.UtcNow };
        if (copyFrom is not null) {
            mapping.Juice = copyFrom.Juice;
            mapping.JuiceDivisional = copyFrom.JuiceDivisional;
            mapping.JuiceConference = copyFrom.JuiceConference;
            mapping.WeeklyCost = copyFrom.WeeklyCost;
        }
        return mapping;
    }
}
