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
///
/// Pure fetch — no caching (frizat-d0t). The settled-slate DB-reconstruction + cache that used to
/// live here (with a bypassCache flag CfbScoresJob set to skip it) moved to
/// CfbCacheService.GetSlateScoresAsync, mirroring where NFL's equivalent already lived
/// (EspnCacheService.GetWeekScoresAsync) — CfbScoresJob now calls this fetcher directly, the same
/// way NflScoresJob already calls INflLiveScoreFetcher directly.
/// </summary>
public interface ICfbLiveScoreFetcher {
    // isCurrentSlate: callers already need to know this themselves (CfbCacheService, to decide
    // whether to bypass its settled cache; CfbScoresJob, resolved once before its slate loop) —
    // passed in rather than re-resolved here, which used to mean a second
    // ICfbCurrentSlateService.GetCurrentSlateAsync() DB round trip on every live fetch just to
    // re-derive a fact the caller already had. Only used here to decide whether to merge in the
    // replay-mode snapshot (see FetchForSlateAsync's own comment).
    Task<EspnScores?> FetchForSlateAsync(CfbSlates slate, bool isCurrentSlate);
}
