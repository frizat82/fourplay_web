using Quartz;

namespace FourPlayWebApp.Server.Jobs;

/// <summary>
/// One Quartz job per sport's scores job, with every fixed time as a trigger on it — so
/// [DisallowConcurrentExecution] keeps every run, scheduled, admin "run now" or queued by
/// FinalScoresWatcher, one at a time. Two runs saving the same finals at once would race on the
/// scores tables' unique index.
/// </summary>
public static class ScoresJobs
{
    public static readonly JobKey Nfl = new("NFL Scores");
    public static readonly JobKey Cfb = new("CFB Scores");

    /// <summary>Set on FinalScoresWatcher's runs: refetch only the current week/slate.</summary>
    public const string CurrentWeekOnlyKey = "currentWeekOnly";
    /// <summary>Which try this on-final run is (1 = the watcher's own).</summary>
    public const string AttemptKey = "attempt";
    /// <summary>Trigger group of the on-final runs (FinalScoresWatcher's, and their retries).</summary>
    public const string OnFinalGroup = "On Final";

    public const int MaxOnFinalAttempts = 3;
    public static readonly TimeSpan OnFinalRetryDelay = TimeSpan.FromMinutes(2);

    public static bool IsCurrentWeekOnly(IJobExecutionContext context) =>
        context.MergedJobDataMap?.TryGetValue(CurrentWeekOnlyKey, out var value) == true && value is true;

    /// <summary>A one-off current-week-only run of <paramref name="job"/>, starting after <paramref name="delay"/>.</summary>
    public static ITrigger OnFinalRun(JobKey job, TimeSpan delay, int attempt = 1) =>
        TriggerBuilder.Create()
            .WithIdentity(Guid.NewGuid().ToString("N"), OnFinalGroup)
            .ForJob(job)
            .UsingJobData(CurrentWeekOnlyKey, true)
            .UsingJobData(AttemptKey, attempt)
            .StartAt(DateTimeOffset.UtcNow + delay)
            .Build();

    /// <summary>
    /// An on-final run is the only quick save for the game that just ended — FinalScoresWatcher
    /// won't queue another for it. If ESPN failed it, queue it again shortly, a few times, rather
    /// than leave that result for the next fixed time. Fixed-time runs aren't retried here.
    /// </summary>
    public static async Task RetryFailedOnFinalRunAsync(IJobExecutionContext context)
    {
        if (!IsCurrentWeekOnly(context)) return;
        var attempt = context.MergedJobDataMap.TryGetValue(AttemptKey, out var value) && value is int n ? n : 1;
        if (attempt >= MaxOnFinalAttempts) return;
        await context.Scheduler.ScheduleJob(
            OnFinalRun(context.JobDetail.Key, OnFinalRetryDelay, attempt + 1), context.CancellationToken);
    }
}
