using FourPlayWebApp.Server.Jobs;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models.Enum;
using NSubstitute;
using Quartz;
using Quartz.Impl.Matchers;

namespace FourPlayWebApp.Server.UnitTests;

// frizat-ugs: thin — mirrors NflSpreadSchedulerJob/CfbSpreadSchedulerJob's shape exactly. Reads
// candidates from LeagueJuiceScheduleSource and registers both the reminder and lock triggers via
// the shared TimedTriggerScheduler.
public class LeagueJuiceSchedulerJobTests
{
    private readonly ILeagueRepository _leagueRepo;
    private readonly ICfbRepository _cfbRepo;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IScheduler _scheduler;
    private readonly IJobExecutionContext _context;

    public LeagueJuiceSchedulerJobTests()
    {
        _leagueRepo = Substitute.For<ILeagueRepository>();
        _cfbRepo = Substitute.For<ICfbRepository>();
        _leagueRepo.GetNflSeasonWeekConfigsAsync().Returns(new List<NflSeasonWeekConfig>());
        _cfbRepo.GetAllWeekConfigsAsync().Returns((IEnumerable<CfbSeasonWeekConfig>)new List<CfbSeasonWeekConfig>());
        _leagueRepo.GetAllLeaguesAsync().Returns(new List<LeagueInfo>());
        _leagueRepo.GetJuiceRemindersSentAsync().Returns(new HashSet<(int, int)>());

        _scheduler = Substitute.For<IScheduler>();
        _schedulerFactory = Substitute.For<ISchedulerFactory>();
        _schedulerFactory.GetScheduler(Arg.Any<CancellationToken>()).Returns(_scheduler);
        _scheduler.GetJobKeys(Arg.Any<GroupMatcher<JobKey>>(), Arg.Any<CancellationToken>()).Returns(new HashSet<JobKey>());
        _context = Substitute.For<IJobExecutionContext>();
    }

    private LeagueJuiceSchedulerJob BuildJob() =>
        new(new LeagueJuiceScheduleSource(_leagueRepo, _cfbRepo, TimeProvider.System), _schedulerFactory);

    private static LeagueInfo MakeLeague(int id, LeagueType type) =>
        new() { Id = id, LeagueName = $"League {id}", OwnerUserId = "owner", LeagueType = type };

    private static NflSeasonWeekConfig MakeNflWeek1(int season, DateTime firstGameUtc) => new() {
        Season = season, WeekId = 1, WeekLabel = "Week 1", WeekType = "Regular Season", ScoringFormat = "Standard",
        FirstGameOfWeekStartDatetime = firstGameUtc,
    };

    [Fact]
    public async Task Execute_SchedulesBothReminderAndLockTriggers_ForEachLeagueSeason()
    {
        _leagueRepo.GetNflSeasonWeekConfigsAsync().Returns([
            MakeNflWeek1(2030, DateTime.UtcNow.AddDays(30)),
        ]);
        _leagueRepo.GetAllLeaguesAsync().Returns([MakeLeague(1, LeagueType.Nfl)]);

        await BuildJob().Execute(_context);

        await _scheduler.Received(1).ScheduleJob(
            Arg.Is<IJobDetail>(j => j.JobType == typeof(LeagueJuiceReminderJob)),
            Arg.Any<ITrigger>(), Arg.Any<CancellationToken>());
        await _scheduler.Received(1).ScheduleJob(
            Arg.Is<IJobDetail>(j => j.JobType == typeof(LeagueJuiceLockJob)),
            Arg.Any<ITrigger>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_NoLeagues_SchedulesNothing()
    {
        await BuildJob().Execute(_context);

        await _scheduler.DidNotReceive().ScheduleJob(Arg.Any<IJobDetail>(), Arg.Any<ITrigger>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenTriggerAlreadyScheduled_IsIdempotent()
    {
        _leagueRepo.GetNflSeasonWeekConfigsAsync().Returns([
            MakeNflWeek1(2030, DateTime.UtcNow.AddDays(30)),
        ]);
        _leagueRepo.GetAllLeaguesAsync().Returns([MakeLeague(1, LeagueType.Nfl)]);
        _scheduler.GetJobKeys(Arg.Any<GroupMatcher<JobKey>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<JobKey> { new("Juice Reminder 1-2030"), new("Juice Lock 1-2030") });

        await BuildJob().Execute(_context);

        await _scheduler.DidNotReceive().ScheduleJob(Arg.Any<IJobDetail>(), Arg.Any<ITrigger>(), Arg.Any<CancellationToken>());
    }
}
