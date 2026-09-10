using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Enum;
using Quartz;
using Serilog;

namespace FourPlayWebApp.Server.Jobs;

[DisallowConcurrentExecution]
public class CfbScoresJob(ICfbLiveScoreFetcher fetcher, ICfbRepository repo, ICfbCacheService cfbCacheService,
    ICfbCurrentSlateService currentSlateService) : IJob {
    // CFB seasons run Aug–Jan; the season year is the calendar year the fall games start.
    private static int Season => DateTime.UtcNow.Month >= 8 ? DateTime.UtcNow.Year : DateTime.UtcNow.Year - 1;

    public async Task Execute(IJobExecutionContext context) {
        Log.Information("CfbScoresJob: fetching CFB scores at {Time}", DateTime.UtcNow);

        var slates = (await repo.GetSlatesForSeasonAsync(Season)).ToList();
        if (slates.Count == 0) {
            Log.Warning("CfbScoresJob: no slates found for season {Season}", Season);
            return;
        }

        // Beyond "slates exist" — are we actually within this season's window right now? Season
        // is a calendar-month cutoff, so slates for the upcoming season can already be seeded
        // while we're still deep in the prior season's off-season; without this check the job
        // would keep hitting ESPN for a not-yet-started or long-finished season.
        var windows = slates.Select(s => new SeasonWindowResolver.Window(
            s.Season, s.StartDate.ToDateTime(TimeOnly.MinValue), s.EndDate.ToDateTime(TimeOnly.MaxValue)));
        if (!SeasonWindowResolver.IsSeasonActive(windows, DateTime.UtcNow)) {
            Log.Information("CfbScoresJob: season {Season} not currently active, skipping ESPN fetch", Season);
            return;
        }

        var scores = new List<CfbScores>();

        // Resolved once for the whole loop, not once per slate — isCurrentSlate only matters to
        // the fetcher for the replay-mode snapshot merge (see ICfbLiveScoreFetcher's own doc
        // comment), and every slate in this loop can be compared against the same single
        // resolution instead of each triggering its own GetCurrentSlateAsync() DB round trip.
        var currentSlate = await currentSlateService.GetCurrentSlateAsync();

        foreach (var slate in slates) {
            // This job's whole purpose is discovering fresh finals — including one missed before
            // the slate's window "ended" (frizat: exactly what happened to the 2026 Week 1
            // Monday-night game before the Tuesday catch-up cron existed). fetcher is now a pure
            // fetch with no caching (frizat-d0t) — the viewer-facing settled-cache shortcut this
            // used to need to bypass now lives one layer up in CfbCacheService, which this job
            // never calls except to invalidate below, mirroring NflScoresJob calling
            // INflLiveScoreFetcher directly.
            var scoreboard = await fetcher.FetchForSlateAsync(slate, isCurrentSlate: slate.Id == currentSlate?.Id);
            if (scoreboard?.Events is null) continue;
            AppendScores(scores, slate, scoreboard.Events);
        }

        if (scores.Count > 0) {
            await repo.UpsertCfbScoresAsync(scores);
            Log.Information("CfbScoresJob: upserted {Count} CFB scores", scores.Count);

            // The viewer-facing cache can already hold a stale reconstruction for a slate that
            // "ended" before this run discovered new/updated finals for it.
            foreach (var slateId in scores.Select(s => s.CfbSlateId).Distinct()) {
                cfbCacheService.InvalidateSlateCache(slateId);
            }
        }
        Log.Information("CfbScoresJob: complete at {Time}", DateTime.UtcNow);
    }

    private static void AppendScores(List<CfbScores> scores, FourPlayWebApp.Server.Models.Data.CfbSlates slate, IEnumerable<FourPlayWebApp.Shared.Models.Event> events) {
        foreach (var evt in events) {
            var comp = evt.Competitions.FirstOrDefault();
            if (comp is null) continue;

            // Only final games are ever persisted — matches NflScoresJob's IsGameOver filter.
            // Live/in-progress display is served separately (never from this DB table), so a
            // half-finished game must never be mistaken for settled data downstream.
            var status = comp.Status.Type.Name;
            if (status != TypeName.StatusFinal) continue;

            var home = comp.Competitors.FirstOrDefault(c => c.HomeAway == HomeAway.Home);
            var away = comp.Competitors.FirstOrDefault(c => c.HomeAway == HomeAway.Away);
            if (home is null || away is null) continue;

            scores.Add(new CfbScores {
                CfbSlateId          = slate.Id,
                HomeTeam            = home.Team.Abbreviation,
                AwayTeam            = away.Team.Abbreviation,
                HomeTeamScore       = (int)home.Score,
                AwayTeamScore       = (int)away.Score,
                GameStatus          = status,
                GameTime            = comp.Date,
                WeatherDisplayValue = evt.Weather?.DisplayValue,
                WeatherConditionId  = evt.Weather?.ConditionId,
                WeatherTemperatureF = evt.Weather?.Temperature,
            });
        }
    }
}
