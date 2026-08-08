using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using Quartz;

namespace FourPlayWebApp.Server.Jobs;

// Resolves frizat-pxy: reads the SpreadLockDatetime column (NflSeasonWeekConfigs) and dynamically
// registers a one-time trigger for NflSpreadJob at that exact instant, replacing the fixed
// Thursday-2pm/Christmas-Eve crons. Runs at startup plus a cheap daily catch-up cron.
[DisallowConcurrentExecution]
public class NflSpreadSchedulerJob(ILeagueRepository repo, ISchedulerFactory schedulerFactory) : IJob {
    public async Task Execute(IJobExecutionContext context) {
        var configs = await repo.GetNflSeasonWeekConfigsAsync();
        var scheduler = await schedulerFactory.GetScheduler(context.CancellationToken);

        var candidates = configs.Select(cfg => (
            LockTime: cfg.SpreadLockDatetime,
            Identity: $"NFL Spreads {cfg.Season} Wk{cfg.WeekId}",
            Description: $"NFL spreads for {cfg.WeekLabel} — scheduled lock time"));

        await SpreadTriggerScheduler.ScheduleFutureTriggersAsync<NflSpreadJob>(scheduler, candidates, context.CancellationToken);
    }
}
