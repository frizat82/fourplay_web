using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Shared.Helpers;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Enum;

namespace FourPlayWebApp.Server.Jobs;

internal static class CfbSlateHelpers {
    // A CFP round is a control-table week whose WeekType is Playoff (frizat-b4g) — its games come
    // from ESPN's CFP endpoint, kept to the week's dates; every other week is a plain date fetch.
    public static bool IsCfpSlate(CfbSeasonWeekConfig slate) => slate.WeekType == WeekType.Playoff;

    // True when at least one team in the game is ranked in the AP Top 25 (curatedRank 1-25).
    // ESPN uses 99 for unranked teams. One input to CfbSpreads.IsLeagueEligible (frizat-9m0) —
    // no longer drops the game outright, since the full FBS slate is always persisted for audit.
    public static bool HasRankedTeam(IEnumerable<Competitor> competitors) =>
        competitors.Any(c => c.CuratedRank?.Current is > 0 and <= 25);

    // Displayable rank (1-25), or null when unranked/absent (ESPN uses 99 for unranked) — the
    // single 1-25 boundary check shared by both the live-ESPN-competitor path and the
    // persisted-CfbRanking-row path (CfbPicksController.GetSpreads reads CfbRanking.CuratedRank,
    // a raw int, back out at request time rather than this app keeping its own second copy).
    public static int? RankOf(int? curatedRank) => curatedRank is > 0 and <= 25 ? curatedRank : null;

    public static int? RankOf(Competitor c) => RankOf(c.CuratedRank?.Current);

    // MACtion — MAC is the only FBS conference that schedules Tue/Wed games; excluded from the
    // league regardless of ranking. Day-of-week check only (ESPN's scoreboard feed as consumed by
    // this app carries no conference field to match on directly). Converts to Eastern before
    // checking, since a UTC Wednesday-early-morning kickoff can be Tuesday night ET.
    public static bool IsMidweekGame(DateTimeOffset gameTime) {
        var dayOfWeekEt = TimeZoneHelpers.ConvertTimeToEt(gameTime).DayOfWeek;
        return dayOfWeekEt is DayOfWeek.Tuesday or DayOfWeek.Wednesday;
    }
}
