using Quartz;
using Quartz.Impl.Matchers;
using Serilog;

namespace FourPlayWebApp.Server.Jobs;

// Shared one-time Quartz trigger registration used by both NflSpreadSchedulerJob and
// CfbSpreadSchedulerJob (frizat-pxy) — dynamically schedules TJob to fire at each candidate's
// lock instant, replacing fixed cron triggers, with data-driven catch-up for past-due weeks.
// Idempotency is a single batch GetJobKeys lookup rather than one scheduler round-trip per
// candidate.
//
// Quartz can't tell "already fired and completed" apart from "never fired" once a one-time
// trigger completes — both just look like "no trigger currently registered" via GetJobKeys — so
// whether a past-due candidate should catch-up-fire is decided from real data state (HasData),
// not Quartz's own internal trigger state.
internal static class SpreadTriggerScheduler {
    public static async Task ScheduleAsync<TJob>(
        IScheduler scheduler,
        IEnumerable<SpreadTriggerCandidate> candidates,
        CancellationToken cancellationToken) where TJob : IJob {

        var now = DateTime.UtcNow;
        var withLockTime = candidates.Where(c => c.LockTime is not null).ToList();
        if (withLockTime.Count == 0) return;

        var existingKeys = (await scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup(), cancellationToken))
            .ToHashSet();

        foreach (var candidate in withLockTime) {
            var lockTime = candidate.LockTime!.Value;
            var jobKey = new JobKey(candidate.Identity);
            if (existingKeys.Contains(jobKey)) continue; // idempotent — already scheduled by a previous run

            var isFuture = lockTime > now;
            if (!isFuture && candidate.HasData) continue; // already succeeded — don't re-fire indefinitely

            var jobDetail = JobBuilder.Create<TJob>()
                .WithIdentity(jobKey)
                .WithDescription(candidate.Description)
                .Build();

            var triggerBuilder = TriggerBuilder.Create()
                .WithIdentity(candidate.Identity)
                .ForJob(jobKey);

            var trigger = isFuture
                ? triggerBuilder.StartAt(new DateTimeOffset(lockTime, TimeSpan.Zero)).Build()
                : triggerBuilder.StartNow().Build();

            await scheduler.ScheduleJob(jobDetail, trigger, cancellationToken);

            if (isFuture)
                Log.Information("SpreadTriggerScheduler: scheduled {Identity} to fire at {LockTime}", candidate.Identity, lockTime);
            else
                Log.Information("SpreadTriggerScheduler: catch-up firing {Identity} now — lock time {LockTime} passed with no data yet", candidate.Identity, lockTime);
        }
    }
}
