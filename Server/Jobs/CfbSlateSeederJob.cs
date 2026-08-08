using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using Quartz;
using Serilog;

namespace FourPlayWebApp.Server.Jobs;

[DisallowConcurrentExecution]
public class CfbSlateSeederJob(ICfbRepository repo, ISchedulerFactory schedulerFactory) : IJob {
    private const int Season = 2026;

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

        await SeedSlatesAsync(configs);

        // Runs regardless of whether slate seeding itself was a no-op — a week's spread-lock
        // trigger still needs (re-)registering even once its slate already exists.
        await ScheduleSpreadTriggersAsync(configs, context);
    }

    private async Task SeedSlatesAsync(List<CfbSeasonWeekConfig> configs) {
        var existing = (await repo.GetSlatesForSeasonAsync(Season)).ToList();
        if (existing.Count >= configs.Count) {
            Log.Information("CfbSlateSeederJob: {Count} slates already seeded for {Season}, skipping", existing.Count, Season);
            return;
        }

        if (existing.Count > 0) {
            await repo.DeleteSlatesAsync(existing);
            Log.Information("CfbSlateSeederJob: removed {Count} stale slates for {Season}", existing.Count, Season);
        }

        var slates = configs.Select(cfg => new CfbSlates {
            Season       = Season,
            SlateNumber  = cfg.IvLeagueWeekNumber,
            Label        = LabelFromConfig(cfg),
            SlateType    = SlateTypeFromConfig(cfg),
            StartDate    = cfg.WeekStartDate,
            EndDate      = cfg.WeekEndDate,
            EspnWeekNumber  = cfg.EspnWeekNumber,
            ScoringFormat   = cfg.ScoringFormat,
        }).ToArray();

        await repo.AddSlatesAsync(slates);
        Log.Information("CfbSlateSeederJob: seeded {Count} slates for {Season}", slates.Length, Season);
    }

    // Resolves frizat-pxy for CFB: reads each in-scope week's SpreadLockDatetime and dynamically
    // registers a one-time trigger for CfbSpreadJob at that exact instant, replacing the fixed
    // Saturday/Wednesday crons (the Wednesday run existed to catch mid-week MAC lines — no longer
    // needed once MAC Tue/Wed games are excluded from the pool, see frizat-9m0).
    private async Task ScheduleSpreadTriggersAsync(List<CfbSeasonWeekConfig> configs, IJobExecutionContext context) {
        var now = DateTime.UtcNow;
        var scheduler = await schedulerFactory.GetScheduler(context.CancellationToken);

        foreach (var cfg in configs.Where(c => c.SpreadLockDatetime is { } lockTime && lockTime > now)) {
            var lockTime = cfg.SpreadLockDatetime!.Value;
            var identity = $"CFB Spreads {cfg.Season} Wk{cfg.IvLeagueWeekNumber}";
            var jobKey = new JobKey(identity);

            if (await scheduler.GetJobDetail(jobKey, context.CancellationToken) is not null) {
                continue; // idempotent — already scheduled by a previous run
            }

            var jobDetail = JobBuilder.Create<CfbSpreadJob>()
                .WithIdentity(jobKey)
                .WithDescription($"CFB spreads for {LabelFromConfig(cfg)} — scheduled lock time")
                .Build();

            var trigger = TriggerBuilder.Create()
                .WithIdentity(identity)
                .ForJob(jobKey)
                .StartAt(new DateTimeOffset(lockTime, TimeSpan.Zero))
                .Build();

            await scheduler.ScheduleJob(jobDetail, trigger, context.CancellationToken);
            Log.Information("CfbSlateSeederJob: scheduled {Identity} to fire at {LockTime}", identity, lockTime);
        }
    }

    private static string LabelFromConfig(CfbSeasonWeekConfig cfg) => cfg.WeekType switch {
        "Conference Championships" => "Conf. Championships",
        "FBS Playoff" => cfg.ScoringFormat switch {
            "NFLDivisional" when cfg.IvLeagueWeekNumber == 15 => "CFP First Round",
            "NFLDivisional" => "CFP Quarterfinals",
            "NFLConference" => "CFP Semifinals",
            "NFLSuperBowl"  => "CFP Championship",
            _ => $"CFP Week {cfg.IvLeagueWeekNumber}",
        },
        _ => $"Week {cfg.IvLeagueWeekNumber}",
    };
}
