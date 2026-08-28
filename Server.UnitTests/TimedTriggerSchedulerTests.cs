using FourPlayWebApp.Server.Jobs;
using NSubstitute;
using Quartz;
using Quartz.Impl.Matchers;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

// TDD, written RED first (frizat-pxy follow-on plan, Phase 3): TimedTriggerScheduler must branch
// three ways per candidate — future lock time schedules normally, past lock time with no data
// fires immediately as catch-up, past lock time with data already present is skipped (already
// succeeded, don't re-fire indefinitely). Tested directly against IScheduler since this is the
// core shared logic both NFL and CFB's schedulers depend on.
public class TimedTriggerSchedulerTests
{
    private readonly IScheduler _scheduler;
    private readonly CancellationToken _token = CancellationToken.None;

    public TimedTriggerSchedulerTests()
    {
        _scheduler = Substitute.For<IScheduler>();
        _scheduler.GetJobKeys(Arg.Any<GroupMatcher<JobKey>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<JobKey>());
    }

    private class FakeSpreadJob : IJob {
        public Task Execute(IJobExecutionContext context) => Task.CompletedTask;
    }

    private class FakeOtherJob : IJob {
        public Task Execute(IJobExecutionContext context) => Task.CompletedTask;
    }

    [Fact]
    public async Task FutureLockTime_SchedulesAtLockTime()
    {
        var lockTime = DateTime.UtcNow.AddDays(3);
        var candidate = new TimedTriggerCandidate(lockTime, "id-1", "desc", HasData: false);

        await TimedTriggerScheduler.ScheduleAsync<FakeSpreadJob>(_scheduler, [candidate], _token);

        await _scheduler.Received(1).ScheduleJob(
            Arg.Is<IJobDetail>(j => j.JobType == typeof(FakeSpreadJob)),
            Arg.Is<ITrigger>(t => t.StartTimeUtc == new DateTimeOffset(lockTime, TimeSpan.Zero)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PastLockTime_NoData_FiresNow()
    {
        var lockTime = DateTime.UtcNow.AddDays(-1);
        var candidate = new TimedTriggerCandidate(lockTime, "id-1", "desc", HasData: false);

        await TimedTriggerScheduler.ScheduleAsync<FakeSpreadJob>(_scheduler, [candidate], _token);

        await _scheduler.Received(1).ScheduleJob(
            Arg.Is<IJobDetail>(j => j.JobType == typeof(FakeSpreadJob)),
            Arg.Any<ITrigger>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PastLockTime_HasData_Skipped()
    {
        var lockTime = DateTime.UtcNow.AddDays(-1);
        var candidate = new TimedTriggerCandidate(lockTime, "id-1", "desc", HasData: true);

        await TimedTriggerScheduler.ScheduleAsync<FakeSpreadJob>(_scheduler, [candidate], _token);

        await _scheduler.DidNotReceive().ScheduleJob(Arg.Any<IJobDetail>(), Arg.Any<ITrigger>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AlreadyRegisteredTrigger_SkippedRegardlessOfCase()
    {
        var lockTime = DateTime.UtcNow.AddDays(3);
        var candidate = new TimedTriggerCandidate(lockTime, "id-1", "desc", HasData: false);
        _scheduler.GetJobKeys(Arg.Any<GroupMatcher<JobKey>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<JobKey> { new("id-1") });

        await TimedTriggerScheduler.ScheduleAsync<FakeSpreadJob>(_scheduler, [candidate], _token);

        await _scheduler.DidNotReceive().ScheduleJob(Arg.Any<IJobDetail>(), Arg.Any<ITrigger>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MultipleCandidates_EachHandledIndependently()
    {
        var candidates = new[] {
            new TimedTriggerCandidate(DateTime.UtcNow.AddDays(3), "future", "desc", HasData: false),
            new TimedTriggerCandidate(DateTime.UtcNow.AddDays(-1), "catchup", "desc", HasData: false),
            new TimedTriggerCandidate(DateTime.UtcNow.AddDays(-1), "done", "desc", HasData: true),
        };

        await TimedTriggerScheduler.ScheduleAsync<FakeSpreadJob>(_scheduler, candidates, _token);

        await _scheduler.Received(2).ScheduleJob(Arg.Any<IJobDetail>(), Arg.Any<ITrigger>(), Arg.Any<CancellationToken>());
    }

    // frizat-ugs: LeagueJuiceReminderJob/LockJob need to know WHICH league fired them — unlike the
    // spread jobs, which resolve "the current week" globally and never needed JobData at all.
    [Fact]
    public async Task JobData_WhenPresent_IsAttachedToTheScheduledJobDetail()
    {
        var candidate = new TimedTriggerCandidate(
            DateTime.UtcNow.AddDays(3), "id-1", "desc", HasData: false,
            JobData: new Dictionary<string, string> { ["LeagueId"] = "42", ["Season"] = "2026" });

        await TimedTriggerScheduler.ScheduleAsync<FakeSpreadJob>(_scheduler, [candidate], _token);

        await _scheduler.Received(1).ScheduleJob(
            Arg.Is<IJobDetail>(j => j.JobDataMap.GetString("LeagueId") == "42" && j.JobDataMap.GetString("Season") == "2026"),
            Arg.Any<ITrigger>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JobData_WhenAbsent_JobDetailHasNoJobData()
    {
        var candidate = new TimedTriggerCandidate(DateTime.UtcNow.AddDays(3), "id-1", "desc", HasData: false);

        await TimedTriggerScheduler.ScheduleAsync<FakeSpreadJob>(_scheduler, [candidate], _token);

        await _scheduler.Received(1).ScheduleJob(
            Arg.Is<IJobDetail>(j => j.JobDataMap.Count == 0),
            Arg.Any<ITrigger>(),
            Arg.Any<CancellationToken>());
    }

    // frizat: a one-time future trigger only ever disappears from GetJobKeys once it actually
    // FIRES — Quartz has no concept of "the source stopped producing this candidate" (e.g. a
    // league got deleted, or a week's config row was corrected away). Without active pruning
    // these orphans sit in the Job Manager forever, which is exactly what surfaced as "6-7 Juice
    // Reminder jobs for 3 leagues" in prod.
    [Fact]
    public async Task StaleJobOfSameType_NoLongerACandidate_IsDeleted()
    {
        var staleKey = new JobKey("stale-id");
        _scheduler.GetJobKeys(Arg.Any<GroupMatcher<JobKey>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<JobKey> { staleKey });
        _scheduler.GetJobDetail(staleKey, Arg.Any<CancellationToken>())
            .Returns(JobBuilder.Create<FakeSpreadJob>().WithIdentity(staleKey).Build());
        // A non-empty candidate list that just doesn't include "stale-id" — pruning is
        // intentionally skipped entirely when the list is empty (see the guard's own comment).
        var stillCurrent = new TimedTriggerCandidate(DateTime.UtcNow.AddDays(3), "still-current", "desc", HasData: false);

        await TimedTriggerScheduler.ScheduleAsync<FakeSpreadJob>(_scheduler, [stillCurrent], _token);

        await _scheduler.Received(1).DeleteJob(staleKey, Arg.Any<CancellationToken>());
    }

    // The empty-candidate-list case is the dangerous one: without this guard, pruning would treat
    // "the source returned nothing" as "delete every already-scheduled job of this type" — a
    // single transient/partial read wiping every future Juice Reminder/Lock or Spread trigger.
    [Fact]
    public async Task EmptyCandidateList_PrunesNothing()
    {
        var existingKey = new JobKey("existing-id");
        _scheduler.GetJobKeys(Arg.Any<GroupMatcher<JobKey>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<JobKey> { existingKey });

        await TimedTriggerScheduler.ScheduleAsync<FakeSpreadJob>(_scheduler, [], _token);

        await _scheduler.DidNotReceive().GetJobDetail(Arg.Any<JobKey>(), Arg.Any<CancellationToken>());
        await _scheduler.DidNotReceive().DeleteJob(Arg.Any<JobKey>(), Arg.Any<CancellationToken>());
    }

    // Multiple job types share one Quartz job store (Juice Reminder, Juice Lock, NFL/CFB Spread
    // all call this same method) — pruning must never touch a job that belongs to a different
    // TJob's candidate source just because it's absent from THIS call's candidate list.
    [Fact]
    public async Task StaleJobOfDifferentType_IsNotDeleted()
    {
        var otherKey = new JobKey("other-id");
        _scheduler.GetJobKeys(Arg.Any<GroupMatcher<JobKey>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<JobKey> { otherKey });
        _scheduler.GetJobDetail(otherKey, Arg.Any<CancellationToken>())
            .Returns(JobBuilder.Create<FakeOtherJob>().WithIdentity(otherKey).Build());
        var stillCurrent = new TimedTriggerCandidate(DateTime.UtcNow.AddDays(3), "still-current", "desc", HasData: false);

        await TimedTriggerScheduler.ScheduleAsync<FakeSpreadJob>(_scheduler, [stillCurrent], _token);

        await _scheduler.DidNotReceive().DeleteJob(Arg.Any<JobKey>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JobStillAmongCurrentCandidates_IsNotPruned()
    {
        var key = new JobKey("id-1");
        var candidate = new TimedTriggerCandidate(DateTime.UtcNow.AddDays(3), "id-1", "desc", HasData: false);
        _scheduler.GetJobKeys(Arg.Any<GroupMatcher<JobKey>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<JobKey> { key });
        _scheduler.GetJobDetail(key, Arg.Any<CancellationToken>())
            .Returns(JobBuilder.Create<FakeSpreadJob>().WithIdentity(key).Build());

        await TimedTriggerScheduler.ScheduleAsync<FakeSpreadJob>(_scheduler, [candidate], _token);

        await _scheduler.DidNotReceive().DeleteJob(Arg.Any<JobKey>(), Arg.Any<CancellationToken>());
    }
}
