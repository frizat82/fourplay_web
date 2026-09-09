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
    // bypassCache: true skips the "slate has ended → replay whatever's already in the DB,
    // cached forever" viewer-facing shortcut and always hits ESPN — CfbScoresJob's whole
    // purpose is to discover NEW finals for a slate, including one that was missed before the
    // slate "ended" (e.g. a scheduling-cron gap); the viewer shortcut exists to spare 100
    // concurrent page loads from re-hitting ESPN for settled data, not to freeze a background
    // catch-up job out of ever seeing fresh data for that slate again.
    Task<EspnScores?> FetchForSlateAsync(CfbSlates slate, bool bypassCache = false);

    // Evicts the cached settled-slate reconstruction so a fresh CfbScoresJob upsert is visible
    // immediately instead of waiting for a process restart.
    void InvalidateSlateCache(int slateId);
}
