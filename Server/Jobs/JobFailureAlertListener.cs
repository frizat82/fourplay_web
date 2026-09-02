using FourPlayWebApp.Server.Services.Interfaces;
using Quartz;

namespace FourPlayWebApp.Server.Jobs;

// frizat-703.2: registered with q.AddJobListener<JobFailureAlertListener>() and no matcher (see
// Program.cs), so this fires for every job uniformly — including jobs that let an exception
// propagate rather than catching it themselves, since Quartz's JobRunShell still reports that
// here as a non-null jobException. One shared mechanism instead of a notifier call bolted onto
// each job's own catch block.
//
// Also centralizes IJobObserverService start/success/failure recording for EVERY job type.
// Originally only LeagueJuiceLockJob/LeagueJuiceReminderJob called RecordJobStartAsync/
// RecordJobSuccessAsync/RecordAndRethrowAsync themselves — every other job (NflScoresJob,
// CfbRankingCaptureJob, the spread jobs, ...) never appeared in the observer at all, so Job
// Manager's "Last Succeeded"/"Last Failed" columns were permanently blank for them regardless of
// whether the job actually ran or restarts happened. A self-reporting job's own richer,
// case-specific message (e.g. "League 5 season 2026 already configured — nothing to lock") is
// never clobbered: this only fills in a generic record when the job didn't already report a
// terminal state (success or failure) for this same run.
public class JobFailureAlertListener(IJobFailureNotifier notifier, IJobObserverService observer) : IJobListener
{
    public string Name => nameof(JobFailureAlertListener);

    public Task JobToBeExecuted(IJobExecutionContext context, CancellationToken cancellationToken = default) =>
        observer.RecordJobStartAsync(context.JobDetail.Key.Name);

    public Task JobExecutionVetoed(IJobExecutionContext context, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public async Task JobWasExecuted(IJobExecutionContext context, JobExecutionException? jobException, CancellationToken cancellationToken = default)
    {
        var jobName = context.JobDetail.Key.Name;
        var info = await observer.GetJobInfoAsync(jobName);

        if (jobException is not null) {
            if (!SelfReportedThisRun(info, info?.LastFailedUtc))
                await observer.RecordJobFailureAsync(jobName, jobException.Message);
            await notifier.NotifyAsync(jobName, context.Trigger.Key.Name, jobException.Message, cancellationToken);
            return;
        }

        if (!SelfReportedThisRun(info, info?.LastSucceededUtc))
            await observer.RecordJobSuccessAsync(jobName);
    }

    private static bool SelfReportedThisRun(JobRunInfo? info, DateTimeOffset? terminalTimeUtc) =>
        info?.LastStartedUtc is { } started && terminalTimeUtc is { } terminal && terminal > started;
}
