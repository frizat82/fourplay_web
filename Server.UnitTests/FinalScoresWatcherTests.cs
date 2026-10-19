using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Quartz;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// Results (and the money that rides on them) used to wait for the next fixed scores-job time —
/// a 3:25 kickoff's final sat unsaved until 7:40pm, Sunday night's until 1am. FinalScoresWatcher
/// listens to the live caches' ScoresChanged events (one shared implementation, both sports) and
/// runs that sport's scores job the moment a game it hasn't seen final before goes final.
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
    }

    private static IConfiguration Config(bool? enabled = null) =>
        new ConfigurationBuilder().AddInMemoryCollection(enabled is null
            ? []
            : [new("FINAL_SCORES_WATCHER_ENABLED", enabled.Value.ToString())]).Build();

    private FinalScoresWatcher BuildWatcher(IConfiguration? config = null) =>
        new(_espnCache, _cfbCache, _schedulerFactory, config ?? Config(), NullLogger<FinalScoresWatcher>.Instance);

    private static Competition Game(string id, bool final) => new()
    {
        Id = id,
        Status = new EspnStatus { Type = new StatusType { Name = final ? TypeName.StatusFinal : TypeName.StatusInProgress, Description = final ? Description.Final : Description.InProgress } },
    };

    private static EspnScores Scoreboard(params Competition[] games) => new()
    {
        Events = games.Select(g => new Event { Id = g.Id, Competitions = new[] { g } }).ToArray(),
    };

    private Task<int> TriggerCount(string jobName) =>
        Task.FromResult(_scheduler.ReceivedCalls().Count(c =>
            c.GetMethodInfo().Name == nameof(IScheduler.TriggerJob) && c.GetArguments()[0] is JobKey k && k.Name == jobName));

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        for (var i = 0; i < 100 && !await condition(); i++) await Task.Delay(20);
    }

    private static async Task SettleAsync() => await Task.Delay(200); // let any (wrongly) queued run land

    [Fact]
    public async Task NflGameGoingFinal_RunsTheNflScoresJobRightAway()
    {
        _espnCache.GetScoresAsync().Returns(Scoreboard(Game("1", final: true), Game("2", final: false)));
        var watcher = BuildWatcher();
        await watcher.StartAsync(CancellationToken.None);

        _espnCache.ScoresChanged += Raise.Event<Action>();
        await WaitUntilAsync(async () => await TriggerCount(FinalScoresWatcher.NflJobName) == 1);

        Assert.Equal(1, await TriggerCount(FinalScoresWatcher.NflJobName));
        Assert.Equal(0, await TriggerCount(FinalScoresWatcher.CfbJobName));
        await watcher.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task CfbGameGoingFinal_RunsTheCfbScoresJob_NotNfl()
    {
        _cfbCache.GetScoresAsync().Returns(Scoreboard(Game("9", final: true)));
        var watcher = BuildWatcher();
        await watcher.StartAsync(CancellationToken.None);

        _cfbCache.ScoresChanged += Raise.Event<Action>();
        await WaitUntilAsync(async () => await TriggerCount(FinalScoresWatcher.CfbJobName) == 1);

        Assert.Equal(1, await TriggerCount(FinalScoresWatcher.CfbJobName));
        Assert.Equal(0, await TriggerCount(FinalScoresWatcher.NflJobName));
        await watcher.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task LivePollsWithNoNewFinal_DoNotRunTheJobAgain()
    {
        _espnCache.GetScoresAsync().Returns(Scoreboard(Game("1", final: true), Game("2", final: false)));
        var watcher = BuildWatcher();
        await watcher.StartAsync(CancellationToken.None);

        _espnCache.ScoresChanged += Raise.Event<Action>();
        await WaitUntilAsync(async () => await TriggerCount(FinalScoresWatcher.NflJobName) == 1);
        // The other game is still being played: every ~15s poll changes its score, not the finals.
        for (var i = 0; i < 3; i++)
        {
            _espnCache.ScoresChanged += Raise.Event<Action>();
            await SettleAsync();
        }

        Assert.Equal(1, await TriggerCount(FinalScoresWatcher.NflJobName));
        await watcher.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ASecondGameGoingFinal_RunsTheJobAgain()
    {
        _espnCache.GetScoresAsync().Returns(Scoreboard(Game("1", final: true), Game("2", final: false)));
        var watcher = BuildWatcher();
        await watcher.StartAsync(CancellationToken.None);
        _espnCache.ScoresChanged += Raise.Event<Action>();
        await WaitUntilAsync(async () => await TriggerCount(FinalScoresWatcher.NflJobName) == 1);

        _espnCache.GetScoresAsync().Returns(Scoreboard(Game("1", final: true), Game("2", final: true)));
        _espnCache.ScoresChanged += Raise.Event<Action>();
        await WaitUntilAsync(async () => await TriggerCount(FinalScoresWatcher.NflJobName) == 2);

        Assert.Equal(2, await TriggerCount(FinalScoresWatcher.NflJobName));
        await watcher.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task NoGameFinalYet_DoesNotRunTheJob()
    {
        _espnCache.GetScoresAsync().Returns(Scoreboard(Game("1", final: false)));
        var watcher = BuildWatcher();
        await watcher.StartAsync(CancellationToken.None);

        _espnCache.ScoresChanged += Raise.Event<Action>();
        await SettleAsync();

        Assert.Equal(0, await TriggerCount(FinalScoresWatcher.NflJobName));
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

        Assert.Equal(0, await TriggerCount(FinalScoresWatcher.NflJobName));
        await watcher.StopAsync(CancellationToken.None);
    }

    // A failed read or trigger must not kill the watcher: the next poll still gets handled.
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
        await WaitUntilAsync(async () => await TriggerCount(FinalScoresWatcher.NflJobName) == 1);

        Assert.Equal(1, await TriggerCount(FinalScoresWatcher.NflJobName));
        await watcher.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task KillSwitch_WhenDisabled_NeverRunsTheJob()
    {
        _espnCache.GetScoresAsync().Returns(Scoreboard(Game("1", final: true)));
        var watcher = BuildWatcher(Config(enabled: false));
        await watcher.StartAsync(CancellationToken.None);

        _espnCache.ScoresChanged += Raise.Event<Action>();
        _cfbCache.ScoresChanged += Raise.Event<Action>();
        await SettleAsync();

        Assert.Empty(_scheduler.ReceivedCalls().Where(c => c.GetMethodInfo().Name == nameof(IScheduler.TriggerJob)));
        await watcher.StopAsync(CancellationToken.None);
    }
}
