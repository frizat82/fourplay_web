using Quartz;
using Quartz.Impl.Matchers;
using Serilog;

namespace FourPlayWebApp.Server.Jobs;

// Shared one-time Quartz trigger registration — originally used only by NflSpreadSchedulerJob and
// CfbSpreadSchedulerJob (frizat-pxy), now also by LeagueJuiceSchedulerJob (frizat-ugs): dynamically
// schedules TJob to fire at each candidate's lock instant, replacing fixed cron triggers, with
// data-driven catch-up for past-due candidates. Idempotency is a single batch GetJobKeys lookup
// rather than one scheduler round-trip per candidate.
//
// Quartz can't tell "already fired and completed" apart from "never fired" once a one-time
// trigger completes — both just look like "no trigger currently registered" via GetJobKeys — so
// whether a past-due candidate should catch-up-fire is decided from real data state (HasData),
// not Quartz's own internal trigger state.
internal static class TimedTriggerScheduler {
    public static async Task ScheduleAsync<TJob>(
        IScheduler scheduler,
        IEnumerable<TimedTriggerCandidate> candidates,
        CancellationToken cancellationToken) where TJob : IJob {

        var now = DateTime.UtcNow;
        var candidateList = candidates.ToList();

        var existingKeys = (await scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup(), cancellationToken))
            .ToHashSet();

        // A one-time future trigger only ever leaves GetJobKeys once it actually FIRES — Quartz
        // has no notion of "the source stopped producing this candidate" (a league got deleted, a
        // week's config row got corrected away). Without this, orphaned jobs sit in the Job
        // Manager forever. Scoped to jobs of TJob's own type — this method is shared by Juice
        // Reminder, Juice Lock, and both spread schedulers against the same job store, so a key
        // absent from this call's candidates must not be assumed to belong to this source.
        var candidateKeys = candidateList.Select(c => new JobKey(c.Identity)).ToHashSet();
        foreach (var staleKey in existingKeys.Except(candidateKeys)) {
            var staleDetail = await scheduler.GetJobDetail(staleKey, cancellationToken);
            if (staleDetail?.JobType != typeof(TJob)) continue;
            await scheduler.DeleteJob(staleKey, cancellationToken);
            Log.Information("TimedTriggerScheduler: pruned stale {Identity} — no longer produced by the candidate source", staleKey.Name);
        }

        if (candidateList.Count == 0) return;

        foreach (var candidate in candidateList) {
            var lockTime = candidate.LockTime;
            var jobKey = new JobKey(candidate.Identity);
            if (existingKeys.Contains(jobKey)) continue; // idempotent — already scheduled by a previous run

            var isFuture = lockTime > now;
            if (!isFuture && candidate.HasData) continue; // already succeeded — don't re-fire indefinitely

            var jobBuilder = JobBuilder.Create<TJob>()
                .WithIdentity(jobKey)
                .WithDescription(candidate.Description);
            if (candidate.JobData is not null) {
                var jobData = new JobDataMap();
                foreach (var (key, value) in candidate.JobData) jobData.Put(key, value);
                jobBuilder.UsingJobData(jobData);
            }
            var jobDetail = jobBuilder.Build();

            var triggerBuilder = TriggerBuilder.Create()
                .WithIdentity(candidate.Identity)
                .ForJob(jobKey);

            var trigger = isFuture
                ? triggerBuilder.StartAt(new DateTimeOffset(lockTime, TimeSpan.Zero)).Build()
                : triggerBuilder.StartNow().Build();

            await scheduler.ScheduleJob(jobDetail, trigger, cancellationToken);

            if (isFuture)
                Log.Information("TimedTriggerScheduler: scheduled {Identity} to fire at {LockTime}", candidate.Identity, lockTime);
            else
                Log.Information("TimedTriggerScheduler: catch-up firing {Identity} now — lock time {LockTime} passed with no data yet", candidate.Identity, lockTime);
        }
    }
}
