using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using Quartz;
using Serilog;

namespace FourPlayWebApp.Server.Jobs;

// Resolves frizat-pxy: reads the SpreadLockDatetime column (NflSeasonWeekConfigs) and dynamically
// registers a one-time trigger for NflSpreadJob at that exact instant, replacing the fixed
// Thursday-2pm/Christmas-Eve crons. Runs at startup plus a cheap daily catch-up cron.
[DisallowConcurrentExecution]
public class NflSpreadSchedulerJob(ILeagueRepository repo, ISchedulerFactory schedulerFactory) : IJob {
    public async Task Execute(IJobExecutionContext context) {
        var configs = await repo.GetNflSeasonWeekConfigsAsync();
        var now = DateTime.UtcNow;
        var scheduler = await schedulerFactory.GetScheduler(context.CancellationToken);

        foreach (var cfg in configs.Where(c => c.SpreadLockDatetime is { } lockTime && lockTime > now)) {
            var lockTime = cfg.SpreadLockDatetime!.Value;
            var identity = $"NFL Spreads {cfg.Season} Wk{cfg.WeekId}";
            var jobKey = new JobKey(identity);

            if (await scheduler.GetJobDetail(jobKey, context.CancellationToken) is not null) {
                continue; // idempotent — already scheduled by a previous run
            }

            var jobDetail = JobBuilder.Create<NflSpreadJob>()
                .WithIdentity(jobKey)
                .WithDescription($"NFL spreads for {cfg.WeekLabel} — scheduled lock time")
                .Build();

            var trigger = TriggerBuilder.Create()
                .WithIdentity(identity)
                .ForJob(jobKey)
                .StartAt(new DateTimeOffset(lockTime, TimeSpan.Zero))
                .Build();

            await scheduler.ScheduleJob(jobDetail, trigger, context.CancellationToken);
            Log.Information("NflSpreadSchedulerJob: scheduled {Identity} to fire at {LockTime}", identity, lockTime);
        }
    }
}
