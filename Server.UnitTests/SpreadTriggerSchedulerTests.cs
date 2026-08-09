using FourPlayWebApp.Server.Jobs;
using NSubstitute;
using Quartz;
using Quartz.Impl.Matchers;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

// TDD, written RED first (frizat-pxy follow-on plan, Phase 3): SpreadTriggerScheduler must branch
// three ways per candidate — future lock time schedules normally, past lock time with no data
// fires immediately as catch-up, past lock time with data already present is skipped (already
// succeeded, don't re-fire indefinitely). Tested directly against IScheduler since this is the
// core shared logic both NFL and CFB's schedulers depend on.
public class SpreadTriggerSchedulerTests
{
    private readonly IScheduler _scheduler;
    private readonly CancellationToken _token = CancellationToken.None;

    public SpreadTriggerSchedulerTests()
    {
        _scheduler = Substitute.For<IScheduler>();
        _scheduler.GetJobKeys(Arg.Any<GroupMatcher<JobKey>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<JobKey>());
    }

    private class FakeSpreadJob : IJob {
        public Task Execute(IJobExecutionContext context) => Task.CompletedTask;
    }

    [Fact]
    public async Task FutureLockTime_SchedulesAtLockTime()
    {
        var lockTime = DateTime.UtcNow.AddDays(3);
        var candidate = new SpreadTriggerCandidate(lockTime, "id-1", "desc", HasData: false);

        await SpreadTriggerScheduler.ScheduleAsync<FakeSpreadJob>(_scheduler, [candidate], _token);

        await _scheduler.Received(1).ScheduleJob(
            Arg.Is<IJobDetail>(j => j.JobType == typeof(FakeSpreadJob)),
            Arg.Is<ITrigger>(t => t.StartTimeUtc == new DateTimeOffset(lockTime, TimeSpan.Zero)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PastLockTime_NoData_FiresNow()
    {
        var lockTime = DateTime.UtcNow.AddDays(-1);
        var candidate = new SpreadTriggerCandidate(lockTime, "id-1", "desc", HasData: false);

        await SpreadTriggerScheduler.ScheduleAsync<FakeSpreadJob>(_scheduler, [candidate], _token);

        await _scheduler.Received(1).ScheduleJob(
            Arg.Is<IJobDetail>(j => j.JobType == typeof(FakeSpreadJob)),
            Arg.Any<ITrigger>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PastLockTime_HasData_Skipped()
    {
        var lockTime = DateTime.UtcNow.AddDays(-1);
        var candidate = new SpreadTriggerCandidate(lockTime, "id-1", "desc", HasData: true);

        await SpreadTriggerScheduler.ScheduleAsync<FakeSpreadJob>(_scheduler, [candidate], _token);

        await _scheduler.DidNotReceive().ScheduleJob(Arg.Any<IJobDetail>(), Arg.Any<ITrigger>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NullLockTime_Skipped()
    {
        var candidate = new SpreadTriggerCandidate(null, "id-1", "desc", HasData: false);

        await SpreadTriggerScheduler.ScheduleAsync<FakeSpreadJob>(_scheduler, [candidate], _token);

        await _scheduler.DidNotReceive().ScheduleJob(Arg.Any<IJobDetail>(), Arg.Any<ITrigger>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AlreadyRegisteredTrigger_SkippedRegardlessOfCase()
    {
        var lockTime = DateTime.UtcNow.AddDays(3);
        var candidate = new SpreadTriggerCandidate(lockTime, "id-1", "desc", HasData: false);
        _scheduler.GetJobKeys(Arg.Any<GroupMatcher<JobKey>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<JobKey> { new("id-1") });

        await SpreadTriggerScheduler.ScheduleAsync<FakeSpreadJob>(_scheduler, [candidate], _token);

        await _scheduler.DidNotReceive().ScheduleJob(Arg.Any<IJobDetail>(), Arg.Any<ITrigger>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MultipleCandidates_EachHandledIndependently()
    {
        var candidates = new[] {
            new SpreadTriggerCandidate(DateTime.UtcNow.AddDays(3), "future", "desc", HasData: false),
            new SpreadTriggerCandidate(DateTime.UtcNow.AddDays(-1), "catchup", "desc", HasData: false),
            new SpreadTriggerCandidate(DateTime.UtcNow.AddDays(-1), "done", "desc", HasData: true),
            new SpreadTriggerCandidate(null, "none", "desc", HasData: false),
        };

        await SpreadTriggerScheduler.ScheduleAsync<FakeSpreadJob>(_scheduler, candidates, _token);

        await _scheduler.Received(2).ScheduleJob(Arg.Any<IJobDetail>(), Arg.Any<ITrigger>(), Arg.Any<CancellationToken>());
    }
}
