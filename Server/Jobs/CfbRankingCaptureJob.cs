using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using Quartz;
using Serilog;

namespace FourPlayWebApp.Server.Jobs;

// CFB-only (no NFL equivalent — ranking/eligibility is a CFB-specific concept, see CLAUDE.md).
// Captures CfbRanking rows as soon as a week's schedule is known — this job runs on
// CfbSlateSeederJob's cadence (startup + weekly Monday), not gated on that week's spread lock like
// CfbSpreadJob's capture is. AP rankings are typically available well before spread lock, and rank
// is exactly what determines IsLeagueEligible — which games users can even see/pick that week.
// Waiting until lock time meant eligibility data was only as fresh as the latest lock.
//
// CfbSpreadJob keeps its own (later) ranking capture too — it's "free" (rides along with the odds
// fetch already being made) and lets a rank move between schedule-known and spread-locked time
// win: AddRankingsAsync upserts one row per (Season, EspnWeekNumber, TeamAbbreviation), so the
// later capture simply overwrites the earlier one rather than appending a second row.
[DisallowConcurrentExecution]
public class CfbRankingCaptureJob(ICfbLiveScoreFetcher fetcher, ICfbRepository repo, TimeProvider timeProvider) : IJob {
    private static int Season => DateTime.UtcNow.Month >= 8 ? DateTime.UtcNow.Year : DateTime.UtcNow.Year - 1;

    public async Task Execute(IJobExecutionContext context) {
        Log.Information("CfbRankingCaptureJob: capturing CFB rankings at {Time}", DateTime.UtcNow);

        var allSlates = (await repo.GetSlatesForSeasonAsync(Season)).ToList();
        if (allSlates.Count == 0) {
            Log.Warning("CfbRankingCaptureJob: no slates found for season {Season} — run CfbSlateSeederJob first", Season);
            return;
        }

        // CfbRankingExtractor only emits rows for StatusScheduled competitions, so a slate whose
        // games are all already final is a guaranteed zero-yield ESPN call — skip it.
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var slates = allSlates.Where(s => s.EndDate >= today).ToList();

        // Each remaining slate's scoreboard fetch is an independent ESPN call — no data dependency
        // between them, so fetch all slates concurrently rather than serializing the round-trips.
        var scoreboards = await Task.WhenAll(slates.Select(async slate =>
            (slate, scoreboard: await fetcher.FetchForSlateAsync(slate))));

        var rankings = new List<CfbRanking>();
        foreach (var (slate, scoreboard) in scoreboards) {
            if (scoreboard?.Events is null) continue;
            rankings.AddRange(CfbRankingExtractor.ExtractFrom(scoreboard.Events, slate));
        }

        if (rankings.Count > 0) {
            await repo.AddRankingsAsync(rankings);
            Log.Information("CfbRankingCaptureJob: saved {Count} CFB rankings", rankings.Count);
        }
        Log.Information("CfbRankingCaptureJob: complete at {Time}", DateTime.UtcNow);
    }
}
