using FourPlayWebApp.Server.Jobs;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using NSubstitute;
using Quartz;
using Quartz.Impl.Matchers;

namespace FourPlayWebApp.Server.UnitTests;

// Moved out of CfbSlateSeederJobTests (frizat-pxy follow-on plan, Phase 2) — CFB's spread-trigger
// scheduling is now its own job, structurally identical to NflSpreadSchedulerJob, not fused into
// slate seeding. See NflSpreadSchedulerJobTests for the sibling NFL coverage.
public class CfbSpreadSchedulerJobTests
{
    private const int Season = 2026;

    private readonly ICfbRepository _repo;
    private readonly IJobExecutionContext _context;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IScheduler _scheduler;

    public CfbSpreadSchedulerJobTests()
    {
        _repo = Substitute.For<ICfbRepository>();
        _repo.GetWeeksWithSpreadDataAsync().Returns(new HashSet<(int, int)>());
        _context = Substitute.For<IJobExecutionContext>();
        _scheduler = Substitute.For<IScheduler>();
        _schedulerFactory = Substitute.For<ISchedulerFactory>();
        _schedulerFactory.GetScheduler(Arg.Any<CancellationToken>()).Returns(_scheduler);
        _scheduler.GetJobKeys(Arg.Any<GroupMatcher<JobKey>>(), Arg.Any<CancellationToken>()).Returns(new HashSet<JobKey>());
    }

    private CfbSpreadSchedulerJob BuildJob() => new(new CfbSpreadScheduleSource(_repo), _schedulerFactory);

    private static List<CfbSeasonWeekConfig> MakeConfigsWithLockTimes(params DateTime?[] lockTimes) =>
        lockTimes.Select((lockTime, i) => new CfbSeasonWeekConfig {
            Season = Season, EspnWeekNumber = i + 1, IvLeagueWeekNumber = i + 1,
            WeekType = "Regular Season", ScoringFormat = "Standard", InScopeIvLeague = true,
            WeekStartDate = new DateOnly(2026, 9, 1), WeekEndDate = new DateOnly(2026, 9, 7),
            SpreadLockDatetime = lockTime,
        }).ToList();

    [Fact]
    public async Task Execute_FutureLockTime_SchedulesCfbSpreadTrigger()
    {
        var lockTime = DateTime.UtcNow.AddDays(3);
        _repo.GetAllWeekConfigsAsync().Returns(MakeConfigsWithLockTimes(lockTime));

        await BuildJob().Execute(_context);

        await _scheduler.Received(1).ScheduleJob(
            Arg.Is<IJobDetail>(j => j.JobType == typeof(CfbSpreadJob)),
            Arg.Is<ITrigger>(t => t.StartTimeUtc == new DateTimeOffset(lockTime, TimeSpan.Zero)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_PastLockTime_NoData_FiresNowAsCatchUp()
    {
        _repo.GetAllWeekConfigsAsync().Returns(MakeConfigsWithLockTimes(DateTime.UtcNow.AddDays(-1)));

        await BuildJob().Execute(_context);

        await _scheduler.Received(1).ScheduleJob(
            Arg.Is<IJobDetail>(j => j.JobType == typeof(CfbSpreadJob)),
            Arg.Any<ITrigger>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_PastLockTime_HasData_Skipped()
    {
        _repo.GetAllWeekConfigsAsync().Returns(MakeConfigsWithLockTimes(DateTime.UtcNow.AddDays(-1)));
        _repo.GetWeeksWithSpreadDataAsync().Returns(new HashSet<(int, int)> { (Season, 1) });

        await BuildJob().Execute(_context);

        await _scheduler.DidNotReceive().ScheduleJob(Arg.Any<IJobDetail>(), Arg.Any<ITrigger>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_NullLockTime_NoTriggerScheduled()
    {
        _repo.GetAllWeekConfigsAsync().Returns(MakeConfigsWithLockTimes((DateTime?)null));

        await BuildJob().Execute(_context);

        await _scheduler.DidNotReceive().ScheduleJob(Arg.Any<IJobDetail>(), Arg.Any<ITrigger>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenTriggerAlreadyScheduled_IsIdempotent()
    {
        _repo.GetAllWeekConfigsAsync().Returns(MakeConfigsWithLockTimes(DateTime.UtcNow.AddDays(3)));
        _scheduler.GetJobKeys(Arg.Any<GroupMatcher<JobKey>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<JobKey> { new($"CFB Spreads {Season} Wk1") });

        await BuildJob().Execute(_context);

        await _scheduler.DidNotReceive().ScheduleJob(Arg.Any<IJobDetail>(), Arg.Any<ITrigger>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_MultipleFutureWeeks_SchedulesTriggerForEach()
    {
        _repo.GetAllWeekConfigsAsync().Returns(MakeConfigsWithLockTimes(DateTime.UtcNow.AddDays(3), DateTime.UtcNow.AddDays(10)));

        await BuildJob().Execute(_context);

        await _scheduler.Received(2).ScheduleJob(Arg.Any<IJobDetail>(), Arg.Any<ITrigger>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_UsesAllSeasons_NotJustCurrent()
    {
        // Regression: trigger scheduling must not be season-scoped like slate seeding is — it
        // should keep working across a season rollover with no code change.
        var futureSeasonLockTime = DateTime.UtcNow.AddDays(3);
        var nextSeasonConfig = new CfbSeasonWeekConfig {
            Season = Season + 1, EspnWeekNumber = 1, IvLeagueWeekNumber = 1,
            WeekType = "Regular Season", ScoringFormat = "Standard", InScopeIvLeague = true,
            WeekStartDate = new DateOnly(2027, 9, 1), WeekEndDate = new DateOnly(2027, 9, 7),
            SpreadLockDatetime = futureSeasonLockTime,
        };
        _repo.GetAllWeekConfigsAsync().Returns([nextSeasonConfig]);

        await BuildJob().Execute(_context);

        await _scheduler.Received(1).ScheduleJob(
            Arg.Is<IJobDetail>(j => j.JobType == typeof(CfbSpreadJob)),
            Arg.Is<ITrigger>(t => t.StartTimeUtc == new DateTimeOffset(futureSeasonLockTime, TimeSpan.Zero)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ExcludesOutOfScopeAndWeek99Configs()
    {
        var configs = new List<CfbSeasonWeekConfig> {
            new() { Season = Season, EspnWeekNumber = 1, IvLeagueWeekNumber = 1, InScopeIvLeague = false,
                WeekType = "Regular Season", ScoringFormat = "Standard", SpreadLockDatetime = DateTime.UtcNow.AddDays(3) },
            new() { Season = Season, EspnWeekNumber = 99, IvLeagueWeekNumber = 99, InScopeIvLeague = true,
                WeekType = "Regular Season", ScoringFormat = "Standard", SpreadLockDatetime = DateTime.UtcNow.AddDays(3) },
        };
        _repo.GetAllWeekConfigsAsync().Returns(configs);

        await BuildJob().Execute(_context);

        await _scheduler.DidNotReceive().ScheduleJob(Arg.Any<IJobDetail>(), Arg.Any<ITrigger>(), Arg.Any<CancellationToken>());
    }
}
