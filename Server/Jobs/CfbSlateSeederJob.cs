using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using Quartz;
using Serilog;

namespace FourPlayWebApp.Server.Jobs;

// Slate seeding only — spread-trigger scheduling lives in CfbSpreadSchedulerJob (frizat-pxy
// follow-on: CFB's scheduler is now structurally identical to NflSpreadSchedulerJob, on its own
// cadence, not fused into this job).
[DisallowConcurrentExecution]
public class CfbSlateSeederJob(ICfbRepository repo) : IJob {
    private static int Season => DateTime.UtcNow.Month >= 8 ? DateTime.UtcNow.Year : DateTime.UtcNow.Year - 1;

    // IvLeagueWeekNumber is the canonical source for SlateType within FBS Playoff weeks
    // because both CFP First Round (IV=15) and Quarterfinals (IV=16) share ScoringFormat="NFLDivisional"
    private static string SlateTypeFromConfig(CfbSeasonWeekConfig cfg) {
        if (cfg.WeekType == "Conference Championships") return "ConferenceChampionship";
        return cfg.ScoringFormat switch {
            "NFLDivisional" when cfg.IvLeagueWeekNumber == 15 => "FirstRound",
            "NFLDivisional" => "Quarterfinal",
            "NFLConference" => "Semifinal",
            "NFLSuperBowl"  => "Championship",
            _ => "RegularSeason",
        };
    }

    public async Task Execute(IJobExecutionContext context) {
        Log.Information("CfbSlateSeederJob: checking season {Season}", Season);

        var configs = (await repo.GetWeekConfigsForSeasonAsync(Season))
            .Where(c => c.InScopeIvLeague && c.IvLeagueWeekNumber != 99)
            .OrderBy(c => c.IvLeagueWeekNumber)
            .ToList();

        if (configs.Count == 0) {
            Log.Warning("CfbSlateSeederJob: no CfbSeasonWeekConfig rows for season {Season} — seed the control table first", Season);
            return;
        }

        var existing = (await repo.GetSlatesForSeasonAsync(Season)).ToList();
        if (existing.Count >= configs.Count) {
            Log.Information("CfbSlateSeederJob: {Count} slates already seeded for {Season}, skipping", existing.Count, Season);
            return;
        }

        if (existing.Count > 0) {
            // frizat-2lc: DeleteSlatesAsync fails closed and skips the delete if any of these
            // slates already carry real dependent CfbSpreads/CfbScores/CfbPicks data — in that
            // case, the whole reseed must be skipped too rather than adding new slates on top of
            // stale ones that couldn't be removed.
            if (!await repo.DeleteSlatesAsync(existing)) {
                Log.Warning("CfbSlateSeederJob: {Count} stale slates for {Season} have dependent spread/score/pick data — skipping reseed", existing.Count, Season);
                return;
            }
            Log.Information("CfbSlateSeederJob: removed {Count} stale slates for {Season}", existing.Count, Season);
        }

        var slates = configs.Select(cfg => new CfbSlates {
            Season       = Season,
            SlateNumber  = cfg.IvLeagueWeekNumber,
            Label        = CfbWeekLabelHelper.LabelFromConfig(cfg),
            SlateType    = SlateTypeFromConfig(cfg),
            StartDate    = cfg.WeekStartDate,
            EndDate      = cfg.WeekEndDate,
            EspnWeekNumber  = cfg.EspnWeekNumber,
            ScoringFormat   = cfg.ScoringFormat,
        }).ToArray();

        await repo.AddSlatesAsync(slates);
        Log.Information("CfbSlateSeederJob: seeded {Count} slates for {Season}", slates.Length, Season);
    }
}
