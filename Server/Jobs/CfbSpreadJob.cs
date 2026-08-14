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
    TimeProvider timeProvider) : IJob {
    public async Task Execute(IJobExecutionContext context) {
        Log.Information("CfbSpreadJob: fetching CFB spreads at {Time}", DateTime.UtcNow);

        // Mirrors NflSpreadJob exactly: fetch only the current slate, not the whole season — a
        // spread job run is "for this week," never a season-wide sweep (frizat CLAUDE.md:
        // siblings, not separate products). Full-season backfill, if ever needed, is a distinct,
        // explicit operation, not this job's default behavior.
        var slateInfo = await currentSlateService.GetCurrentSlateAsync();
        if (slateInfo is null) {
            Log.Warning("CfbSpreadJob: no current slate found — run CfbSlateSeederJob first");
            return;
        }

        if (SpreadLockGuard.ShouldSkip(slateInfo.SpreadLockDatetime, timeProvider.GetUtcNow().UtcDateTime, context)) {
            Log.Information("CfbSpreadJob: skipping {Label} — lock time {LockTime} not yet reached", slateInfo.Label, slateInfo.SpreadLockDatetime);
            return;
        }

        var slate = await repo.GetSlateByIdAsync(slateInfo.Id);
        if (slate is null) {
            Log.Warning("CfbSpreadJob: slate {SlateId} vanished between resolution and fetch", slateInfo.Id);
            return;
        }

        var spreads = new List<CfbSpreads>();
        var rankings = new List<CfbRanking>();

        var scoreboard = await fetcher.FetchForSlateAsync(slate);
        if (scoreboard?.Events is not null) {
            rankings.AddRange(CfbRankingExtractor.ExtractFrom(scoreboard.Events, slate));
            await ProcessEventsForSpreads(spreads, slate, scoreboard.Events);
        }

        if (spreads.Count > 0) {
            await repo.UpsertAsync(spreads);
            Log.Information("CfbSpreadJob: saved {Count} CFB spreads", spreads.Count);
        }
        if (rankings.Count > 0) {
            await repo.AddRankingsAsync(rankings);
            Log.Information("CfbSpreadJob: saved {Count} CFB rankings", rankings.Count);
        }
        Log.Information("CfbSpreadJob: complete at {Time}", DateTime.UtcNow);
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
                var odds = await oddsService.GetCfbEventsWithOddsAsync(eventId, (int)EspnOddsProviders.DraftKings);
                if (odds is null) {
                    var allOdds = await oddsService.GetCfbEventsWithOddsAsync(eventId);
                    if (allOdds is null || allOdds.Count == 0) {
                        Log.Warning("CfbSpreadJob: no odds for event {EventId} ({Home} vs {Away})", eventId, home, away);
                        continue;
                    }
                    odds = allOdds.Items.First();
                }

                var homeSpread = odds.HomeTeamOdds.Current.PointSpread.American.Replace("+", "");
                var awaySpread = odds.AwayTeamOdds.Current.PointSpread.American.Replace("+", "");
                if (!double.TryParse(homeSpread, out var parsedHome)) continue;
                if (!double.TryParse(awaySpread, out var parsedAway)) continue;

                spreads.Add(new CfbSpreads {
                    CfbSlateId    = slate.Id,
                    HomeTeam      = home,
                    AwayTeam      = away,
                    HomeTeamSpread = parsedHome,
                    AwayTeamSpread = parsedAway,
                    OverUnder     = odds.OverUnder,
                    GameTime      = comp.Date,
                    IsLeagueEligible = isEligible,
                });
            } catch (Exception ex) {
                Log.Error(ex, "CfbSpreadJob: error fetching odds for event {EventId}", eventId);
            }
        }
    }
}
