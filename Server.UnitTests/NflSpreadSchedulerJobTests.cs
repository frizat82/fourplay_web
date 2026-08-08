using FourPlayWebApp.Server.Jobs;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using NSubstitute;
using Quartz;

namespace FourPlayWebApp.Server.UnitTests;

public class NflSpreadSchedulerJobTests
{
    private readonly ILeagueRepository _repo;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IScheduler _scheduler;
    private readonly IJobExecutionContext _context;

    public NflSpreadSchedulerJobTests()
    {
        _repo = Substitute.For<ILeagueRepository>();
        _scheduler = Substitute.For<IScheduler>();
        _schedulerFactory = Substitute.For<ISchedulerFactory>();
        _schedulerFactory.GetScheduler(Arg.Any<CancellationToken>()).Returns(_scheduler);
        _context = Substitute.For<IJobExecutionContext>();
    }

    private NflSpreadSchedulerJob BuildJob() => new(_repo, _schedulerFactory);

    private static NflSeasonWeekConfig MakeConfig(int season, int weekId, DateTime? spreadLock, string label = "Week") =>
        new() { Season = season, WeekId = weekId, WeekLabel = label, SpreadLockDatetime = spreadLock };

    [Fact]
    public async Task Execute_FutureLockTime_SchedulesTrigger()
    {
        var lockTime = DateTime.UtcNow.AddDays(3);
        _repo.GetNflSeasonWeekConfigsAsync().Returns([MakeConfig(2026, 6, lockTime)]);
        _scheduler.GetJobDetail(Arg.Any<JobKey>()).Returns((IJobDetail?)null);

        await BuildJob().Execute(_context);

        await _scheduler.Received(1).ScheduleJob(
            Arg.Is<IJobDetail>(j => j.JobType == typeof(NflSpreadJob)),
            Arg.Is<ITrigger>(t => t.StartTimeUtc == new DateTimeOffset(lockTime, TimeSpan.Zero)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_PastLockTime_NoTrigger()
    {
        var lockTime = DateTime.UtcNow.AddDays(-1);
        _repo.GetNflSeasonWeekConfigsAsync().Returns([MakeConfig(2026, 6, lockTime)]);

        await BuildJob().Execute(_context);

        await _scheduler.DidNotReceive().ScheduleJob(Arg.Any<IJobDetail>(), Arg.Any<ITrigger>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_NullLockTime_Skipped()
    {
        _repo.GetNflSeasonWeekConfigsAsync().Returns([MakeConfig(2026, 6, null)]);

        await BuildJob().Execute(_context);

        await _scheduler.DidNotReceive().ScheduleJob(Arg.Any<IJobDetail>(), Arg.Any<ITrigger>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenTriggerAlreadyScheduled_IsIdempotent()
    {
        var lockTime = DateTime.UtcNow.AddDays(3);
        _repo.GetNflSeasonWeekConfigsAsync().Returns([MakeConfig(2026, 6, lockTime)]);
        _scheduler.GetJobDetail(Arg.Any<JobKey>()).Returns(Substitute.For<IJobDetail>());

        await BuildJob().Execute(_context);

        await _scheduler.DidNotReceive().ScheduleJob(Arg.Any<IJobDetail>(), Arg.Any<ITrigger>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_MultipleFutureWeeks_SchedulesEachWithDistinctKey()
    {
        _repo.GetNflSeasonWeekConfigsAsync().Returns([
            MakeConfig(2026, 6, DateTime.UtcNow.AddDays(3)),
            MakeConfig(2026, 7, DateTime.UtcNow.AddDays(10)),
        ]);
        _scheduler.GetJobDetail(Arg.Any<JobKey>()).Returns((IJobDetail?)null);

        var scheduledKeys = new List<JobKey>();
        await _scheduler.ScheduleJob(
            Arg.Do<IJobDetail>(j => scheduledKeys.Add(j.Key)),
            Arg.Any<ITrigger>(),
            Arg.Any<CancellationToken>());

        await BuildJob().Execute(_context);

        Assert.Equal(2, scheduledKeys.Distinct().Count());
    }

    [Fact]
    public async Task Execute_MixOfPastAndFutureWeeks_OnlySchedulesFuture()
    {
        _repo.GetNflSeasonWeekConfigsAsync().Returns([
            MakeConfig(2026, 5, DateTime.UtcNow.AddDays(-3)),
            MakeConfig(2026, 6, DateTime.UtcNow.AddDays(3)),
        ]);
        _scheduler.GetJobDetail(Arg.Any<JobKey>()).Returns((IJobDetail?)null);

        await BuildJob().Execute(_context);

        await _scheduler.Received(1).ScheduleJob(Arg.Any<IJobDetail>(), Arg.Any<ITrigger>(), Arg.Any<CancellationToken>());
    }
}
