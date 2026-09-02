using FourPlayWebApp.Server.Jobs;
using FourPlayWebApp.Server.Services.Interfaces;
using NSubstitute;
using Quartz;

namespace FourPlayWebApp.Server.UnitTests;

// frizat-703.2: a single Quartz IJobListener registered with no matcher (see Program.cs) so it
// fires for every job's failure uniformly — including jobs that let an exception propagate
// rather than catching it themselves, since Quartz's JobRunShell still reports that here as a
// non-null jobException. One shared mechanism, not a notifier call bolted onto each job.
//
// It now also centralizes IJobObserverService start/success/failure recording for EVERY job
// type — previously only LeagueJuiceLockJob/LeagueJuiceReminderJob called RecordJobStartAsync/
// RecordJobSuccessAsync/RecordAndRethrowAsync themselves, so every other job (NflScoresJob,
// CfbRankingCaptureJob, etc.) never appeared in the observer at all and Job Manager's
// "Last Succeeded"/"Last Failed" columns were permanently blank for them, restart or not.
public class JobFailureAlertListenerTests
{
    private readonly IJobFailureNotifier _notifier = Substitute.For<IJobFailureNotifier>();
    private readonly IJobObserverService _observer = Substitute.For<IJobObserverService>();
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

    private JobFailureAlertListener BuildListener() => new(_notifier, _observer);

    [Fact]
    public async Task JobToBeExecuted_RecordsStartForEveryJob()
    {
        await BuildListener().JobToBeExecuted(_context);

        await _observer.Received(1).RecordJobStartAsync("NflSpreadJob");
    }

    [Fact]
    public async Task JobWasExecuted_WithException_NotifiesWithJobAndTriggerNames()
    {
        var exception = new JobExecutionException("ESPN odds API timed out");

        await BuildListener().JobWasExecuted(_context, exception);

        await _notifier.Received(1).NotifyAsync("NflSpreadJob", "NFL Spreads 2026 Wk1", "ESPN odds API timed out");
    }

    [Fact]
    public async Task JobWasExecuted_WithException_RecordsFailure_WhenJobDidNotAlreadySelfReport()
    {
        var exception = new JobExecutionException("ESPN odds API timed out");
        _observer.GetJobInfoAsync("NflSpreadJob").Returns(new JobRunInfo("NflSpreadJob") { LastStartedUtc = DateTimeOffset.UtcNow });

        await BuildListener().JobWasExecuted(_context, exception);

        await _observer.Received(1).RecordJobFailureAsync("NflSpreadJob", "ESPN odds API timed out");
    }

    [Fact]
    public async Task JobWasExecuted_WithException_SkipsFailureRecording_WhenJobAlreadySelfReportedThisRun()
    {
        // Mirrors LeagueJuiceLockJob/LeagueJuiceReminderJob's own catch block calling
        // RecordAndRethrowAsync before this listener ever sees the exception — LastFailedUtc is
        // already newer than LastStartedUtc for this run.
        var started = DateTimeOffset.UtcNow.AddSeconds(-1);
        _observer.GetJobInfoAsync("NflSpreadJob").Returns(new JobRunInfo("NflSpreadJob") {
            LastStartedUtc = started,
            LastFailedUtc = started.AddMilliseconds(500),
        });

        await BuildListener().JobWasExecuted(_context, new JobExecutionException("boom"));

        await _observer.DidNotReceive().RecordJobFailureAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task JobWasExecuted_WithoutException_DoesNotNotify()
    {
        await BuildListener().JobWasExecuted(_context, jobException: null);

        await _notifier.DidNotReceive().NotifyAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task JobWasExecuted_WithoutException_RecordsSuccess_WhenJobDidNotAlreadySelfReport()
    {
        // The typical case for most job types (NflScoresJob, CfbRankingCaptureJob, ...) — they
        // never call RecordJobSuccessAsync themselves, so without this the observer would never
        // learn they ran at all.
        _observer.GetJobInfoAsync("NflSpreadJob").Returns(new JobRunInfo("NflSpreadJob") { LastStartedUtc = DateTimeOffset.UtcNow });

        await BuildListener().JobWasExecuted(_context, jobException: null);

        await _observer.Received(1).RecordJobSuccessAsync("NflSpreadJob", null);
    }

    [Fact]
    public async Task JobWasExecuted_WithoutException_SkipsSuccessRecording_WhenJobAlreadySelfReportedThisRun()
    {
        // A job like LeagueJuiceLockJob already called RecordJobSuccessAsync itself with a
        // richer, case-specific message ("League 5 season 2026 already configured — nothing to
        // lock") — the listener must not clobber that with a generic one.
        var started = DateTimeOffset.UtcNow.AddSeconds(-1);
        _observer.GetJobInfoAsync("NflSpreadJob").Returns(new JobRunInfo("NflSpreadJob") {
            LastStartedUtc = started,
            LastSucceededUtc = started.AddMilliseconds(500),
        });

        await BuildListener().JobWasExecuted(_context, jobException: null);

        await _observer.DidNotReceive().RecordJobSuccessAsync(Arg.Any<string>(), Arg.Any<string?>());
    }
}
