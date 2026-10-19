using FourPlayWebApp.Server.Jobs;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Quartz;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// Results (and the money that rides on them) used to wait for the next fixed scores-job time.
/// FinalScoresWatcher queues that sport's scores job the moment a game it hasn't seen final goes
/// final — see its own doc comment.
/// </summary>
public class FinalScoresWatcherTests
{
    private readonly IEspnCacheService _espnCache = Substitute.For<IEspnCacheService>();
    private readonly ICfbCacheService _cfbCache = Substitute.For<ICfbCacheService>();
    private readonly ISchedulerFactory _schedulerFactory = Substitute.For<ISchedulerFactory>();
    private readonly IScheduler _scheduler = Substitute.For<IScheduler>();

    public FinalScoresWatcherTests()
    {
        _schedulerFactory.GetScheduler(Arg.Any<CancellationToken>()).Returns(_scheduler);
        _scheduler.CheckExists(Arg.Any<JobKey>(), Arg.Any<CancellationToken>()).Returns(true);
        _scheduler.GetTriggersOfJob(Arg.Any<JobKey>(), Arg.Any<CancellationToken>()).Returns(new List<ITrigger>());
    }

    private FinalScoresWatcher BuildWatcher(bool enabled = true) =>
        new(_espnCache, _cfbCache, _schedulerFactory,
            new ConfigurationBuilder().AddInMemoryCollection([new("FINAL_SCORES_WATCHER_ENABLED", enabled.ToString())]).Build(),
            NullLogger<FinalScoresWatcher>.Instance);

    private static Competition Game(string id, bool final) => new()
    {
        Id = id,
        Status = new EspnStatus { Type = new StatusType { Name = final ? TypeName.StatusFinal : TypeName.StatusInProgress, Description = final ? Description.Final : Description.InProgress } },
    };

    private static EspnScores Scoreboard(params Competition[] games) => new()
    {
        Events = games.Select(g => new Event { Id = g.Id, Competitions = new[] { g } }).ToArray(),
    };

    /// <summary>Runs queued for this job (IScheduler.ScheduleJob(ITrigger) calls).</summary>
    private List<ITrigger> Queued(JobKey job) => _scheduler.ReceivedCalls()
        .Where(c => c.GetMethodInfo().Name == nameof(IScheduler.ScheduleJob))
        .Select(c => c.GetArguments()[0]).OfType<ITrigger>()
        .Where(t => job.Equals(t.JobKey)).ToList();

    private async Task WaitForQueuedAsync(JobKey job, int count)
    {
        for (var i = 0; i < 100 && Queued(job).Count < count; i++) await Task.Delay(20);
    }

    private static Task SettleAsync() => Task.Delay(200); // let any (wrongly) queued run land

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AGameGoingFinal_QueuesThatSportsScoresJob_ForTheCurrentWeekOnly(bool cfb)
    {
        var (cache, job, other) = cfb
            ? ((ILiveScoresSource)_cfbCache, ScoresJobs.Cfb, ScoresJobs.Nfl)
            : (_espnCache, ScoresJobs.Nfl, ScoresJobs.Cfb);
        cache.GetScoresAsync().Returns(Scoreboard(Game("1", final: true), Game("2", final: false)));
        var watcher = BuildWatcher();
        await watcher.StartAsync(CancellationToken.None);

        cache.ScoresChanged += Raise.Event<Action>();
        await WaitForQueuedAsync(job, 1);

        var trigger = Assert.Single(Queued(job));
        Assert.Equal(ScoresJobs.OnFinalGroup, trigger.Key.Group);
        Assert.True(trigger.JobDataMap.GetBoolean(ScoresJobs.CurrentWeekOnlyKey));
        // A short wait, so games ending a minute apart share one run.
        Assert.InRange(trigger.StartTimeUtc - DateTimeOffset.UtcNow, TimeSpan.FromSeconds(30), FinalScoresWatcher.BatchDelay);
        Assert.Empty(Queued(other));
        await watcher.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task LivePollsWithNoNewFinal_DoNotQueueAnotherRun()
    {
        _espnCache.GetScoresAsync().Returns(Scoreboard(Game("1", final: true), Game("2", final: false)));
        var watcher = BuildWatcher();
        await watcher.StartAsync(CancellationToken.None);

        _espnCache.ScoresChanged += Raise.Event<Action>();
        await WaitForQueuedAsync(ScoresJobs.Nfl, 1);
        // The other game is still being played: every ~15s poll changes its score, not the finals.
        for (var i = 0; i < 3; i++)
        {
            _espnCache.ScoresChanged += Raise.Event<Action>();
            await SettleAsync();
        }

        Assert.Single(Queued(ScoresJobs.Nfl));
        await watcher.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ASecondGameGoingFinal_QueuesAnotherRun()
    {
        _espnCache.GetScoresAsync().Returns(Scoreboard(Game("1", final: true), Game("2", final: false)));
        var watcher = BuildWatcher();
        await watcher.StartAsync(CancellationToken.None);
        _espnCache.ScoresChanged += Raise.Event<Action>();
        await WaitForQueuedAsync(ScoresJobs.Nfl, 1);

        _espnCache.GetScoresAsync().Returns(Scoreboard(Game("1", final: true), Game("2", final: true)));
        _espnCache.ScoresChanged += Raise.Event<Action>();
        await WaitForQueuedAsync(ScoresJobs.Nfl, 2);

        Assert.Equal(2, Queued(ScoresJobs.Nfl).Count);
        await watcher.StopAsync(CancellationToken.None);
    }

    // A run is already waiting to start: it reads ESPN when it does, so it saves this final too.
    [Fact]
    public async Task ANewFinal_WhileARunIsQueuedButNotStarted_DoesNotQueueAnother()
    {
        GivenAnOnFinalTrigger(nextFire: DateTimeOffset.UtcNow.AddSeconds(30));
        _espnCache.GetScoresAsync().Returns(Scoreboard(Game("1", final: true)));
        var watcher = BuildWatcher();
        await watcher.StartAsync(CancellationToken.None);

        _espnCache.ScoresChanged += Raise.Event<Action>();
        await SettleAsync();

        Assert.Empty(Queued(ScoresJobs.Nfl));
        await watcher.StopAsync(CancellationToken.None);
    }

    // A run that already started may have read ESPN before this game ended — queue another.
    [Fact]
    public async Task ANewFinal_WhileARunIsAlreadyRunning_QueuesAnother()
    {
        GivenAnOnFinalTrigger(nextFire: null);
        _espnCache.GetScoresAsync().Returns(Scoreboard(Game("1", final: true)));
        var watcher = BuildWatcher();
        await watcher.StartAsync(CancellationToken.None);

        _espnCache.ScoresChanged += Raise.Event<Action>();
        await WaitForQueuedAsync(ScoresJobs.Nfl, 1);

        Assert.Single(Queued(ScoresJobs.Nfl));
        await watcher.StopAsync(CancellationToken.None);
    }

    private void GivenAnOnFinalTrigger(DateTimeOffset? nextFire)
    {
        var existing = Substitute.For<ITrigger>();
        existing.Key.Returns(new TriggerKey("earlier", ScoresJobs.OnFinalGroup));
        existing.GetNextFireTimeUtc().Returns(nextFire);
        _scheduler.GetTriggersOfJob(ScoresJobs.Nfl, Arg.Any<CancellationToken>()).Returns(new List<ITrigger> { existing });
    }

    [Fact]
    public async Task NoGameFinalYet_DoesNotQueueARun()
    {
        _espnCache.GetScoresAsync().Returns(Scoreboard(Game("1", final: false)));
        var watcher = BuildWatcher();
        await watcher.StartAsync(CancellationToken.None);

        _espnCache.ScoresChanged += Raise.Event<Action>();
        await SettleAsync();

        Assert.Empty(Queued(ScoresJobs.Nfl));
        await watcher.StopAsync(CancellationToken.None);
    }

    // Demo/replay stacks don't register the scores jobs at all (Program.cs) — nothing to run.
    [Fact]
    public async Task WhenTheScoresJobIsNotRegistered_DoesNothingAndDoesNotThrow()
    {
        _scheduler.CheckExists(Arg.Any<JobKey>(), Arg.Any<CancellationToken>()).Returns(false);
        _espnCache.GetScoresAsync().Returns(Scoreboard(Game("1", final: true)));
        var watcher = BuildWatcher();
        await watcher.StartAsync(CancellationToken.None);

        _espnCache.ScoresChanged += Raise.Event<Action>();
        await SettleAsync();

        Assert.Empty(Queued(ScoresJobs.Nfl));
        await watcher.StopAsync(CancellationToken.None);
    }

    // A failed read or queue must not kill the watcher: the next poll still gets handled.
    [Fact]
    public async Task AFailedScoreboardRead_IsRetriedOnTheNextPoll()
    {
        _espnCache.GetScoresAsync().Returns(
            _ => throw new HttpRequestException("ESPN down"),
            _ => Scoreboard(Game("1", final: true)));
        var watcher = BuildWatcher();
        await watcher.StartAsync(CancellationToken.None);

        _espnCache.ScoresChanged += Raise.Event<Action>();
        await SettleAsync();
        _espnCache.ScoresChanged += Raise.Event<Action>();
        await WaitForQueuedAsync(ScoresJobs.Nfl, 1);

        Assert.Single(Queued(ScoresJobs.Nfl));
        await watcher.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task KillSwitch_WhenDisabled_NeverQueuesARun()
    {
        _espnCache.GetScoresAsync().Returns(Scoreboard(Game("1", final: true)));
        _cfbCache.GetScoresAsync().Returns(Scoreboard(Game("9", final: true)));
        var watcher = BuildWatcher(enabled: false);
        await watcher.StartAsync(CancellationToken.None);

        _espnCache.ScoresChanged += Raise.Event<Action>();
        _cfbCache.ScoresChanged += Raise.Event<Action>();
        await SettleAsync();

        Assert.Empty(Queued(ScoresJobs.Nfl));
        Assert.Empty(Queued(ScoresJobs.Cfb));
        await watcher.StopAsync(CancellationToken.None);
    }

    // HasQueuedRunAsync relies on how Quartz reports a one-shot trigger: waiting to fire, it has a
    // next fire time; once fired (its run executing), it's still listed but has none.
    [Fact]
    public async Task Quartz_AFiredOneShotTrigger_HasNoNextFireTime_WhileItsRunExecutes()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddQuartz(q => q.AddJob<BlockingJob>(j => j.WithIdentity("Blocking").StoreDurably()));
        var scheduler = await services.BuildServiceProvider().GetRequiredService<ISchedulerFactory>().GetScheduler();
        BlockingJob.Started = new TaskCompletionSource();
        BlockingJob.Release = new TaskCompletionSource();
        await scheduler.Start();
        try
        {
            var job = new JobKey("Blocking");
            await scheduler.ScheduleJob(TriggerBuilder.Create().WithIdentity("later", ScoresJobs.OnFinalGroup)
                .ForJob(job).StartAt(DateTimeOffset.UtcNow.AddHours(1)).Build());
            Assert.NotNull(Assert.Single(await scheduler.GetTriggersOfJob(job)).GetNextFireTimeUtc());

            await scheduler.ScheduleJob(TriggerBuilder.Create().WithIdentity("now", ScoresJobs.OnFinalGroup)
                .ForJob(job).StartNow().Build());
            await BlockingJob.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));

            var fired = (await scheduler.GetTriggersOfJob(job)).SingleOrDefault(t => t.Key.Name == "now");
            Assert.True(fired is null || fired.GetNextFireTimeUtc() is null);
        }
        finally
        {
            BlockingJob.Release.TrySetResult();
            await scheduler.Shutdown(waitForJobsToComplete: true);
        }
    }

    [DisallowConcurrentExecution]
    private sealed class BlockingJob : IJob
    {
        public static TaskCompletionSource Started = new();
        public static TaskCompletionSource Release = new();

        public async Task Execute(IJobExecutionContext context)
        {
            Started.TrySetResult();
            await Release.Task;
        }
    }
}
