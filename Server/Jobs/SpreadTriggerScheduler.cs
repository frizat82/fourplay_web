using Quartz;
using Quartz.Impl.Matchers;
using Serilog;

namespace FourPlayWebApp.Server.Jobs;

// Shared one-time Quartz trigger registration used by both NflSpreadSchedulerJob and
// CfbSlateSeederJob (frizat-pxy/frizat-9m0) — dynamically schedules TJob to fire at each
// candidate's lock instant, replacing fixed cron triggers. Idempotency is a single batch
// GetJobKeys lookup rather than one scheduler round-trip per candidate.
internal static class SpreadTriggerScheduler {
    public static async Task ScheduleFutureTriggersAsync<TJob>(
        IScheduler scheduler,
        IEnumerable<(DateTime? LockTime, string Identity, string Description)> candidates,
        CancellationToken cancellationToken) where TJob : IJob {

        var now = DateTime.UtcNow;
        var due = candidates.Where(c => c.LockTime is { } lockTime && lockTime > now).ToList();
        if (due.Count == 0) return;

        var existingKeys = (await scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup(), cancellationToken))
            .ToHashSet();

        foreach (var (lockTime, identity, description) in due) {
            var jobKey = new JobKey(identity);
            if (existingKeys.Contains(jobKey)) continue; // idempotent — already scheduled by a previous run

            var jobDetail = JobBuilder.Create<TJob>()
                .WithIdentity(jobKey)
                .WithDescription(description)
                .Build();

            var trigger = TriggerBuilder.Create()
                .WithIdentity(identity)
                .ForJob(jobKey)
                .StartAt(new DateTimeOffset(lockTime!.Value, TimeSpan.Zero))
                .Build();

            await scheduler.ScheduleJob(jobDetail, trigger, cancellationToken);
            Log.Information("SpreadTriggerScheduler: scheduled {Identity} to fire at {LockTime}", identity, lockTime);
        }
    }
}
