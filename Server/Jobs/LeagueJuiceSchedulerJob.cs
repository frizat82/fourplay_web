using Quartz;

namespace FourPlayWebApp.Server.Jobs;

// frizat-ugs: mirrors NflSpreadSchedulerJob/CfbSpreadSchedulerJob's shape exactly — reads
// candidates from LeagueJuiceScheduleSource (itself driven entirely by whatever season rows exist
// in NflSeasonWeekConfigs/CfbSeasonWeekConfigs, never a hardcoded year) and registers both the
// reminder and lock one-time triggers via the shared TimedTriggerScheduler. Runs at startup plus a
// daily catch-up cron, same cadence as the spread schedulers.
[DisallowConcurrentExecution]
public class LeagueJuiceSchedulerJob(LeagueJuiceScheduleSource source, ISchedulerFactory schedulerFactory) : IJob {
    public async Task Execute(IJobExecutionContext context) {
        var (reminders, locks) = await source.GetCandidatesAsync();
        var scheduler = await schedulerFactory.GetScheduler(context.CancellationToken);
        await TimedTriggerScheduler.ScheduleAsync<LeagueJuiceReminderJob>(scheduler, reminders, context.CancellationToken);
        await TimedTriggerScheduler.ScheduleAsync<LeagueJuiceLockJob>(scheduler, locks, context.CancellationToken);
    }
}
