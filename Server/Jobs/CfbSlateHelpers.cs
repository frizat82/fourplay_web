using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Server.Jobs;

internal static class CfbSlateHelpers {
    // True for CFP postseason rounds (ESPN seasontype=3), false for regular season/conf-champs
    // (seasontype=2) — callers use this to pick which ICfbApiService method to call.
    public static bool IsCfpSlate(string? scoringFormat) =>
        scoringFormat is "NFLDivisional" or "NFLConference" or "NFLSuperBowl";

    // True when at least one team in the game is ranked in the AP Top 25 (curatedRank 1-25).
    // ESPN uses 99 for unranked teams. Filters out Army/Navy/Group-of-5 games from week queries.
    public static bool HasRankedTeam(IEnumerable<Competitor> competitors) =>
        competitors.Any(c => c.CuratedRank?.Current is > 0 and <= 25);
}
