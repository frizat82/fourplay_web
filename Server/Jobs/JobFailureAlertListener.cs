using FourPlayWebApp.Server.Services.Interfaces;
using Quartz;

namespace FourPlayWebApp.Server.Jobs;

// frizat-703.2: registered with q.AddJobListener<JobFailureAlertListener>() and no matcher (see
// Program.cs), so this fires for every job's failure uniformly — including jobs that let an
// exception propagate rather than catching it themselves, since Quartz's JobRunShell still
// reports that here as a non-null jobException. One shared mechanism instead of a notifier call
// bolted onto each job's own catch block.
public class JobFailureAlertListener(IJobFailureNotifier notifier) : IJobListener
{
    public string Name => nameof(JobFailureAlertListener);

    public Task JobToBeExecuted(IJobExecutionContext context, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task JobExecutionVetoed(IJobExecutionContext context, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public async Task JobWasExecuted(IJobExecutionContext context, JobExecutionException? jobException, CancellationToken cancellationToken = default)
    {
        if (jobException is null) return;

        await notifier.NotifyAsync(context.JobDetail.Key.Name, context.Trigger.Key.Name, jobException.Message, cancellationToken);
    }
}
