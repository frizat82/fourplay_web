using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Enum;
using Quartz;
using Serilog;

namespace FourPlayWebApp.Server.Jobs;

[DisallowConcurrentExecution]
public class CfbSpreadJob(
    ICfbLiveScoreFetcher fetcher,
    IEspnCoreOddsService oddsService,
    ICfbRepository repo,
    ICfbCurrentSlateService currentSlateService,
    TimeProvider timeProvider,
    IJobObserverService observer) : IJob {
    public async Task Execute(IJobExecutionContext context) {
        // This job persists what it reads, so it always reads ESPN fresh (see EspnDayCache).
        using var freshEspn = EspnDayCache.Fresh();
        var jobName = context.JobDetail.Key.Name;
        try {
            Log.Information("CfbSpreadJob: fetching CFB spreads at {Time}", DateTime.UtcNow);

            // Mirrors NflSpreadJob exactly: fetch only the current slate, not the whole season —
            // a spread job run is "for this week," never a season-wide sweep (frizat CLAUDE.md:
            // siblings, not separate products). Full-season backfill, if ever needed, is a
            // distinct, explicit operation, not this job's default behavior.
            var slateInfo = await currentSlateService.GetCurrentSlateAsync();
            if (slateInfo is null) {
                Log.Warning("CfbSpreadJob: no current slate found — run CfbSlateSeederJob first");
                await observer.RecordJobSuccessAsync(jobName, "No current slate found");
                return;
            }

            if (SpreadLockGuard.ShouldSkip(slateInfo.SpreadLockDatetime, timeProvider.GetUtcNow().UtcDateTime, context)) {
                Log.Information("CfbSpreadJob: skipping {Label} — lock time {LockTime} not yet reached", slateInfo.Label, slateInfo.SpreadLockDatetime);
                await observer.RecordJobSuccessAsync(jobName, $"Skipped {slateInfo.Label} — lock time {slateInfo.SpreadLockDatetime} not yet reached");
                return;
            }

            var slate = await repo.GetSlateByIdAsync(slateInfo.Id);
            if (slate is null) {
                Log.Warning("CfbSpreadJob: slate {SlateId} vanished between resolution and fetch", slateInfo.Id);
                await observer.RecordJobSuccessAsync(jobName, $"Slate {slateInfo.Id} vanished between resolution and fetch");
                return;
            }

            var spreads = new List<CfbSpreads>();
            var rankings = new List<CfbRanking>();

            // slate was resolved from slateInfo.Id above, i.e. it IS the current slate.
            var scoreboard = await fetcher.FetchForSlateAsync(slate, isCurrentSlate: true);
            if (scoreboard is null) {
                throw new InvalidOperationException(
                    $"CfbSpreadJob: ESPN fetch failed entirely for {slate.Label} (past lock time {slateInfo.SpreadLockDatetime}) — no scoreboard data retrieved");
            }

            if (scoreboard.Events is not null) {
                rankings.AddRange(CfbRankingExtractor.ExtractFrom(scoreboard.Events, slate));
                await ProcessEventsForSpreads(spreads, slate, scoreboard.Events);
            }

            // Persist whatever we actually got — e.g. rankings can be genuinely available even on
            // a run where every game's own odds fetch failed — BEFORE deciding below whether this
            // run counts as a failure. A partial success should never be thrown away just because
            // the run also gets flagged as needing attention.
            if (spreads.Count > 0) {
                await repo.UpsertAsync(spreads);
                Log.Information("CfbSpreadJob: saved {Count} CFB spreads", spreads.Count);
            }
            if (rankings.Count > 0) {
                await repo.AddRankingsAsync(rankings);
                Log.Information("CfbSpreadJob: saved {Count} CFB rankings", rankings.Count);
            }
            Log.Information("CfbSpreadJob: complete at {Time}", DateTime.UtcNow);

            // frizat-4gn: mirrors NflSpreadJob exactly — the schedule exists specifically so this
            // job runs once real games' odds should already be posted for the slate's full
            // lineup. Every in-scope CFB slate (regular season through CFP Championship) has at
            // least one real game — there's no legitimate reason for spreads.Count to end up 0
            // here, whether that's from a genuinely empty scoreboard.Events or every game's own
            // odds fetch failing. Throwing lets the existing JobFailureAlertListener/
            // DiscordJobFailureNotifier pipeline actually fire, same as every other job already
            // relies on.
            if (spreads.Count == 0) {
                throw new InvalidOperationException(
                    $"CfbSpreadJob: {slate.Label} is past lock time {slateInfo.SpreadLockDatetime}, but zero spreads were saved");
            }

            await observer.RecordJobSuccessAsync(jobName, $"Saved {spreads.Count} spreads, {rankings.Count} rankings for {slate.Label}");
        } catch (Exception ex) {
            await observer.RecordAndRethrowAsync(jobName, ex);
        }
    }

    private async Task ProcessEventsForSpreads(
        List<CfbSpreads> spreads,
        CfbSlates slate,
        IEnumerable<Event> events) {
        var isCfp = CfbSlateHelpers.IsCfpSlate(slate.ScoringFormat);

        foreach (var evt in events) {
            var comp = evt.Competitions.FirstOrDefault();
            if (comp is null || comp.Status.Type.Name != TypeName.StatusScheduled) continue;

            var eventId = int.Parse(evt.Id);
            var home = comp.Competitors.FirstOrDefault(c => c.HomeAway == HomeAway.Home)?.Team.Abbreviation ?? "";
            var away = comp.Competitors.FirstOrDefault(c => c.HomeAway == HomeAway.Away)?.Team.Abbreviation ?? "";

            var isEligible = isCfp
                || (CfbSlateHelpers.HasRankedTeam(comp.Competitors) && !CfbSlateHelpers.IsMidweekGame(comp.Date));

            try {
                var parsed = await SpreadOddsFetcher.FetchAsync(
                    oddsService.GetCfbEventsWithOddsAsync, oddsService.GetCfbEventsWithOddsAsync, eventId, $"{home} vs {away}");
                if (parsed is null) continue;

                spreads.Add(new CfbSpreads {
                    CfbSlateId    = slate.Id,
                    HomeTeam      = home,
                    AwayTeam      = away,
                    HomeTeamSpread = parsed.Value.HomeSpread,
                    AwayTeamSpread = parsed.Value.AwaySpread,
                    OverUnder     = parsed.Value.OverUnder,
                    GameTime      = comp.Date,
                    IsLeagueEligible = isEligible,
                });
            } catch (Exception ex) {
                Log.Error(ex, "CfbSpreadJob: error fetching odds for event {EventId}", eventId);
            }
        }
    }
}
