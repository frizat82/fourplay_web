using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Server.Services.Interfaces;

/// <summary>
/// Resolves the live ESPN scoreboard for one CFB slate — CFP week=999 + date filter for postseason,
/// full-slate date-range query (frizat-11t: slate.StartDate/EndDate, not ESPN's own week=N bucket)
/// for regular season/conf-champs, no rank filter at ingestion (frizat-9m0 — eligibility is computed
/// downstream). A slate missing EspnWeekNumber is a control-table seeding bug, not a case to fall
/// back on — logs a warning and returns null. Extracted from
/// CfbScoresJob (frizat-703.6) so the DB-upsert jobs (CfbScoresJob, CfbSpreadJob) and
/// CfbCacheService's live-serving path share one implementation instead of maintaining the
/// CFP/ranked branching three times.
/// </summary>
public interface ICfbLiveScoreFetcher {
    Task<EspnScores?> FetchForSlateAsync(CfbSlates slate);
}
