using FourPlayWebApp.Shared.Models.Data;

namespace FourPlayWebApp.Shared.Helpers;

public static class CfbSpreadsExtensions {
    // Single definition of "eligible for serving" (frizat-9m0) — the full FBS slate is always
    // persisted for audit; this is what narrows it back down for picks/scores pages.
    public static IEnumerable<CfbSpreads> WhereLeagueEligible(this IEnumerable<CfbSpreads> spreads) =>
        spreads.Where(s => s.IsLeagueEligible);

    public static IEnumerable<CfbSpreads> WhereLeagueIneligible(this IEnumerable<CfbSpreads> spreads) =>
        spreads.Where(s => !s.IsLeagueEligible);
}
