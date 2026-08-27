using FourPlayWebApp.Server.Jobs;
using FourPlayWebApp.Server.Services.Interfaces;
using NSubstitute;
using Quartz;

namespace FourPlayWebApp.Server.UnitTests;

// frizat-703.2: a single Quartz IJobListener registered with no matcher (see Program.cs) so it
// fires for every job's failure uniformly — including jobs that let an exception propagate
// rather than catching it themselves, since Quartz's JobRunShell still reports that here as a
// non-null jobException. One shared mechanism, not a notifier call bolted onto each job.
public class JobFailureAlertListenerTests
{
    private readonly IJobFailureNotifier _notifier = Substitute.For<IJobFailureNotifier>();
    private readonly IJobExecutionContext _context = Substitute.For<IJobExecutionContext>();
    private readonly IJobDetail _jobDetail = Substitute.For<IJobDetail>();
    private readonly ITrigger _trigger = Substitute.For<ITrigger>();

    public JobFailureAlertListenerTests()
    {
        _jobDetail.Key.Returns(new JobKey("NflSpreadJob"));
        _trigger.Key.Returns(new TriggerKey("NFL Spreads 2026 Wk1"));
        _context.JobDetail.Returns(_jobDetail);
        _context.Trigger.Returns(_trigger);
    }

    private JobFailureAlertListener BuildListener() => new(_notifier);

    [Fact]
    public async Task JobWasExecuted_WithException_NotifiesWithJobAndTriggerNames()
    {
        var exception = new JobExecutionException("ESPN odds API timed out");

        await BuildListener().JobWasExecuted(_context, exception);

        await _notifier.Received(1).NotifyAsync("NflSpreadJob", "NFL Spreads 2026 Wk1", "ESPN odds API timed out");
    }

    [Fact]
    public async Task JobWasExecuted_WithoutException_DoesNotNotify()
    {
        await BuildListener().JobWasExecuted(_context, jobException: null);

        await _notifier.DidNotReceive().NotifyAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }
}
