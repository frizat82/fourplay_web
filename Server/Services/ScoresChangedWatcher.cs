using System.Threading.Channels;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models.Enum;
using Microsoft.Extensions.Hosting;

namespace FourPlayWebApp.Server.Services;

/// <summary>
/// Shared plumbing for background services that react to a live cache's scoreboard changing, for
/// both sports. ScoresChanged fires synchronously inside each cache's ~15s poll loop, so the
/// handler must not do the work inline: it only signals a per-sport bounded (capacity 1)
/// channel, which coalesces a burst of events into a single pending call to
/// OnScoresChangedAsync. A config kill switch turns a watcher off without a deploy.
/// </summary>
public abstract class ScoresChangedWatcher : BackgroundService
{
    private readonly ILogger _logger;
    private readonly string _killSwitchKey;
    private readonly bool _enabled;
    private readonly SportFeed[] _feeds;

    protected ScoresChangedWatcher(
        IEspnCacheService espnCache,
        ICfbCacheService cfbCache,
        IConfiguration configuration,
        string killSwitchKey,
        ILogger logger)
    {
        _logger = logger;
        _killSwitchKey = killSwitchKey;
        _enabled = configuration.GetValue(killSwitchKey, true);
        _feeds = [new SportFeed(LeagueType.Nfl, espnCache), new SportFeed(LeagueType.Cfb, cfbCache)];
    }

    /// <summary>One sport's scoreboard changed (one call per coalesced burst, never concurrently for a sport).</summary>
    protected abstract Task OnScoresChangedAsync(LeagueType sport, ILiveScoresSource source, CancellationToken stoppingToken);

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        if (_enabled)
        {
            foreach (var feed in _feeds) feed.Source.ScoresChanged += feed.Signal;
        }
        else
        {
            _logger.LogInformation("{Watcher} disabled via {KillSwitch}=false", GetType().Name, _killSwitchKey);
        }
        return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var feed in _feeds) feed.Source.ScoresChanged -= feed.Signal;
        return base.StopAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        _enabled ? Task.WhenAll(_feeds.Select(feed => DrainAsync(feed, stoppingToken))) : Task.CompletedTask;

    private async Task DrainAsync(SportFeed feed, CancellationToken stoppingToken)
    {
        await foreach (var _ in feed.Pending.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await OnScoresChangedAsync(feed.Sport, feed.Source, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // The next change retries.
                _logger.LogError(ex, "{Watcher}: handling a {Sport} scoreboard change failed", GetType().Name, feed.Sport);
            }
        }
    }

    private sealed class SportFeed
    {
        public SportFeed(LeagueType sport, ILiveScoresSource source)
        {
            Sport = sport;
            Source = source;
            Signal = () => Pending.Writer.TryWrite(true);
        }

        public LeagueType Sport { get; }
        public ILiveScoresSource Source { get; }
        public Channel<bool> Pending { get; } = Channel.CreateBounded<bool>(
            new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });
        /// <summary>The ScoresChanged handler — one instance, so StopAsync unsubscribes the same delegate.</summary>
        public Action Signal { get; }
    }
}
