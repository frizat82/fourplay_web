using FourPlayWebApp.Shared.Helpers;
using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Server.Jobs;

internal static class CfbSlateHelpers {
    // True for CFP postseason rounds (ESPN seasontype=3), false for regular season/conf-champs
    // (seasontype=2) — callers use this to pick which ICfbApiService method to call.
    public static bool IsCfpSlate(string? scoringFormat) =>
        scoringFormat is "NFLDivisional" or "NFLConference" or "NFLSuperBowl";

    // True when at least one team in the game is ranked in the AP Top 25 (curatedRank 1-25).
    // ESPN uses 99 for unranked teams. One input to CfbSpreads.IsLeagueEligible (frizat-9m0) —
    // no longer drops the game outright, since the full FBS slate is always persisted for audit.
    public static bool HasRankedTeam(IEnumerable<Competitor> competitors) =>
        competitors.Any(c => c.CuratedRank?.Current is > 0 and <= 25);

    // MACtion — MAC is the only FBS conference that schedules Tue/Wed games; excluded from the
    // league regardless of ranking. Day-of-week check only (ESPN's scoreboard feed as consumed by
    // this app carries no conference field to match on directly). Converts to Eastern before
    // checking, since a UTC Wednesday-early-morning kickoff can be Tuesday night ET.
    public static bool IsMidweekGame(DateTimeOffset gameTime) {
        var dayOfWeekEt = TimeZoneHelpers.ConvertTimeToEt(gameTime).DayOfWeek;
        return dayOfWeekEt is DayOfWeek.Tuesday or DayOfWeek.Wednesday;
    }
}
