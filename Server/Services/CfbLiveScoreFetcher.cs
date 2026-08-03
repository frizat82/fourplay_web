using FourPlayWebApp.Server.Jobs;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models;
using Serilog;

namespace FourPlayWebApp.Server.Services;

public class CfbLiveScoreFetcher(ICfbApiService cfbApi) : ICfbLiveScoreFetcher {
    public async Task<EspnScores?> FetchForSlateAsync(CfbSlates slate) {
        // CfbSeasonWeekConfig.EspnWeekNumber is non-nullable, and both known producers of CfbSlates
        // rows (CfbSlateSeederJob, DemoDataSeeder) always copy a real week number through — so every
        // slate in practice carries one, and this app only ever makes week-based ESPN queries, never
        // date-range "scoreboard" calls. CfbSlates.EspnWeekNumber itself is still nullable at the
        // model/DB level though, so a future producer could leave it unset — a missing value here
        // means the control table wasn't seeded correctly, not a case to silently paper over with a
        // date-range fallback.
        if (!slate.EspnWeekNumber.HasValue) {
            Log.Warning("CfbLiveScoreFetcher: slate {SlateId} has no EspnWeekNumber — skipping", slate.Id);
            return null;
        }
        return CfbSlateHelpers.IsCfpSlate(slate.ScoringFormat)
            ? await FetchCfpAsync(slate)
            : await FetchRankedWeekAsync(slate);
    }

    private async Task<EspnScores?> FetchCfpAsync(CfbSlates slate) {
        // CFP: week=999 ESPN bucket returns all CFP games; filter to this round by date
        var scoreboard = await cfbApi.GetCfpGamesAsync();
        if (scoreboard?.Events is null) return null;

        var events = scoreboard.Events.Where(e => {
            var comp = e.Competitions.FirstOrDefault();
            return comp is not null
                && comp.Date.Date >= slate.StartDate.ToDateTime(TimeOnly.MinValue).Date
                && comp.Date.Date <= slate.EndDate.ToDateTime(TimeOnly.MaxValue).Date;
        }).ToArray();

        return events.Length == 0 ? null : WithEvents(scoreboard, events);
    }

    private async Task<EspnScores?> FetchRankedWeekAsync(CfbSlates slate) {
        // Regular season / conf-champs: filter to ranked teams only
        var scoreboard = await cfbApi.GetScoresByWeekAsync(slate.EspnWeekNumber!.Value, isPostSeason: false);
        if (scoreboard?.Events is null) return null;

        var events = scoreboard.Events
            .Where(e => e.Competitions.Any(c => CfbSlateHelpers.HasRankedTeam(c.Competitors)))
            .ToArray();

        return events.Length == 0 ? null : WithEvents(scoreboard, events);
    }

    private static EspnScores WithEvents(EspnScores source, Event[] events) => new() {
        Leagues = source.Leagues,
        Season  = source.Season,
        Week    = source.Week,
        Events  = events,
    };
}
