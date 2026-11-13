using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using Quartz;
using Serilog;

namespace FourPlayWebApp.Server.Jobs;

// CFB-only (no NFL equivalent — ranking/eligibility is a CFB-specific concept, see CLAUDE.md).
// Captures the current week's CfbRanking rows from that week's poll — this job runs at startup,
// weekly Monday and daily, not gated on that week's spread lock like CfbSpreadJob's capture is.
// AP rankings are out well before spread lock, and rank is exactly what determines
// IsLeagueEligible — which games users can even see/pick that week.
//
// CfbSpreadJob keeps its own (later) ranking capture too — it's "free" (rides along with the odds
// fetch already being made) and lets a rank move between schedule-known and spread-locked time
// win: AddRankingsAsync upserts one row per (Season, WeekNumber, TeamAbbreviation), so the
// later capture simply overwrites the earlier one rather than appending a second row.
[DisallowConcurrentExecution]
public class CfbRankingCaptureJob(ICfbLiveScoreFetcher fetcher, ICfbRepository repo, TimeProvider timeProvider) : IJob {
    private static int Season => DateTime.UtcNow.Month >= 8 ? DateTime.UtcNow.Year : DateTime.UtcNow.Year - 1;

    public async Task Execute(IJobExecutionContext context) {
        // This job persists what it reads, so it always reads ESPN fresh (see EspnDayCache).
        using var freshEspn = EspnDayCache.Fresh();
        Log.Information("CfbRankingCaptureJob: capturing CFB rankings at {Time}", DateTime.UtcNow);

        var allSlates = (await repo.GetSlatesForSeasonAsync(Season)).ToList();
        if (allSlates.Count == 0) {
            Log.Warning("CfbRankingCaptureJob: no in-scope CfbSeasonWeekConfigs rows for season {Season}", Season);
            return;
        }

        // Only the current week (the first unfinished slate, resolved the same way the rest of the
        // app does): its ranks come from this week's poll. Capturing later weeks with today's poll
        // was a guess that went stale (Northwestern's Nov 1 #20 sat in weeks 10-13 after it fell
        // out of the Top 25). The first run after a week ends can still see last week's poll; the
        // Monday run, after the new poll is out, upserts and clears to match it.
        var windows = CfbCurrentSlateService.BuildSlateWindows(allSlates);
        var current = SeasonWindowResolver.FirstUnfinishedWeek(
            windows.Select(w => w.Window), Season, timeProvider.GetUtcNow().UtcDateTime);
        if (current is not { } window) {
            Log.Information("CfbRankingCaptureJob: season {Season} is over, nothing to capture", Season);
            return;
        }
        var slate = windows.First(w => w.Window == window).Slate;

        // isCurrentSlate: false — this job extracts AP rankings from real ESPN events, never wants
        // the replay-mode snapshot merged in.
        var scoreboard = await fetcher.FetchForSlateAsync(slate, isCurrentSlate: false);
        if (scoreboard?.Events is null) {
            Log.Warning("CfbRankingCaptureJob: ESPN returned no games for {Week}; ranks not refreshed", slate.WeekLabel);
            return;
        }
        var capture = CfbRankingExtractor.ExtractFrom(scoreboard.Events, slate);

        if (capture.Ranked.Count > 0) {
            await repo.AddRankingsAsync(capture.Ranked);
            Log.Information("CfbRankingCaptureJob: saved {Count} CFB rankings for {Week}", capture.Ranked.Count, slate.WeekLabel);
        }
        if (capture.Unranked.Count > 0) {
            var removed = await repo.RemoveRankingsAsync(capture.Unranked);
            Log.Information("CfbRankingCaptureJob: cleared {Count} ranks of teams that dropped out of the poll", removed);
        }
        Log.Information("CfbRankingCaptureJob: complete at {Time}", DateTime.UtcNow);
    }
}
