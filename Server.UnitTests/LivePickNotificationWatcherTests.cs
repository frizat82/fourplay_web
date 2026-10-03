using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models.Enum;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// LivePickNotificationWatcher is the only sport-specific wiring in the whole feature — it
/// subscribes to IEspnCacheService's and ICfbCacheService's ScoresChanged events and routes both
/// into the one shared ILivePickTransitionService.RecomputeAsync, via a per-recompute DI scope
/// (since the watcher itself is a long-lived singleton/hosted service).
/// </summary>
public class LivePickNotificationWatcherTests
{
    private readonly IEspnCacheService _espnCache = Substitute.For<IEspnCacheService>();
    private readonly ICfbCacheService _cfbCache = Substitute.For<ICfbCacheService>();
    private readonly ILivePickTransitionService _transitionService = Substitute.For<ILivePickTransitionService>();

    private static IConfiguration Config(bool? enabled = null) =>
        new ConfigurationBuilder().AddInMemoryCollection(enabled is null
            ? []
            : [new("LIVE_PICK_NOTIFICATIONS_ENABLED", enabled.Value.ToString())]).Build();

    private LivePickNotificationWatcher BuildWatcher(IConfiguration? config = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(_transitionService);
        var provider = services.BuildServiceProvider();
        return new LivePickNotificationWatcher(_espnCache, _cfbCache,
            provider.GetRequiredService<IServiceScopeFactory>(), config ?? Config(),
            NullLogger<LivePickNotificationWatcher>.Instance);
    }

    [Fact]
    public async Task NflScoresChanged_RoutesToRecomputeAsync_ForNfl()
    {
        var watcher = BuildWatcher();
        await watcher.StartAsync(CancellationToken.None); // subscribes to events AND kicks off ExecuteAsync's drain loop

        _espnCache.ScoresChanged += Raise.Event<Action>();

        await WaitForCallAsync(() => _transitionService.ReceivedCalls().Any());

        await _transitionService.Received().RecomputeAsync(LeagueType.Nfl, Arg.Any<CancellationToken>());
        await _transitionService.DidNotReceive().RecomputeAsync(LeagueType.Cfb, Arg.Any<CancellationToken>());

        await watcher.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task CfbScoresChanged_RoutesToRecomputeAsync_ForCfb()
    {
        var watcher = BuildWatcher();
        await watcher.StartAsync(CancellationToken.None);

        _cfbCache.ScoresChanged += Raise.Event<Action>();

        await WaitForCallAsync(() => _transitionService.ReceivedCalls().Any());

        await _transitionService.Received().RecomputeAsync(LeagueType.Cfb, Arg.Any<CancellationToken>());
        await _transitionService.DidNotReceive().RecomputeAsync(LeagueType.Nfl, Arg.Any<CancellationToken>());

        await watcher.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task KillSwitch_WhenDisabled_NeverSubscribesOrRecomputes()
    {
        var watcher = BuildWatcher(Config(enabled: false));
        await watcher.StartAsync(CancellationToken.None);

        _espnCache.ScoresChanged += Raise.Event<Action>();
        _cfbCache.ScoresChanged += Raise.Event<Action>();
        await Task.Delay(200); // give any (wrongly-fired) recompute a chance to land

        await _transitionService.DidNotReceive().RecomputeAsync(Arg.Any<LeagueType>(), Arg.Any<CancellationToken>());

        await watcher.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAsync_UnsubscribesFromBothCacheServices()
    {
        var watcher = BuildWatcher();
        await watcher.StartAsync(CancellationToken.None);
        await watcher.StopAsync(CancellationToken.None);

        _espnCache.ScoresChanged += Raise.Event<Action>();
        _cfbCache.ScoresChanged += Raise.Event<Action>();
        await Task.Delay(200);

        await _transitionService.DidNotReceive().RecomputeAsync(Arg.Any<LeagueType>(), Arg.Any<CancellationToken>());
    }

    private static async Task WaitForCallAsync(Func<bool> condition)
    {
        for (var i = 0; i < 50 && !condition(); i++)
            await Task.Delay(20);
        Assert.True(condition(), "Expected RecomputeAsync to have been called within the timeout.");
    }
}
