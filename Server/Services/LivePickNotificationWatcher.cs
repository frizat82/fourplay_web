using System.Threading.Channels;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models.Enum;
using Microsoft.Extensions.Hosting;

namespace FourPlayWebApp.Server.Services;

/// <summary>
/// Subscribes to both IEspnCacheService's and ICfbCacheService's ScoresChanged events — the only
/// sport-specific wiring in the whole feature — and routes each into the one shared
/// ILivePickTransitionService.RecomputeAsync. ScoresChanged fires synchronously inside each cache
/// service's ~15s poll loop, so the event handler must not run the recompute inline: it only
/// signals a per-sport bounded (capacity 1) channel, which coalesces a burst of events into a
/// single pending recompute rather than queuing one per event.
/// </summary>
public class LivePickNotificationWatcher : BackgroundService
{
    private readonly IEspnCacheService _espnCache;
    private readonly ICfbCacheService _cfbCache;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LivePickNotificationWatcher> _logger;
    private readonly bool _enabled;
    private readonly Channel<bool> _nflSignal = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });
    private readonly Channel<bool> _cfbSignal = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    public LivePickNotificationWatcher(
        IEspnCacheService espnCache,
        ICfbCacheService cfbCache,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<LivePickNotificationWatcher> logger)
    {
        _espnCache = espnCache;
        _cfbCache = cfbCache;
        _scopeFactory = scopeFactory;
        _logger = logger;
        // Kill switch: disable without a deploy if real-world cost turns out worse than expected.
        _enabled = configuration.GetValue("LIVE_PICK_NOTIFICATIONS_ENABLED", true);
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        if (_enabled)
        {
            _espnCache.ScoresChanged += OnNflScoresChanged;
            _cfbCache.ScoresChanged += OnCfbScoresChanged;
        }
        else
        {
            _logger.LogInformation("LivePickNotificationWatcher disabled via LIVE_PICK_NOTIFICATIONS_ENABLED=false");
        }
        return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _espnCache.ScoresChanged -= OnNflScoresChanged;
        _cfbCache.ScoresChanged -= OnCfbScoresChanged;
        return base.StopAsync(cancellationToken);
    }

    private void OnNflScoresChanged() => _nflSignal.Writer.TryWrite(true);
    private void OnCfbScoresChanged() => _cfbSignal.Writer.TryWrite(true);

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled) return Task.CompletedTask;
        return Task.WhenAll(
            DrainAsync(_nflSignal, LeagueType.Nfl, stoppingToken),
            DrainAsync(_cfbSignal, LeagueType.Cfb, stoppingToken));
    }

    private async Task DrainAsync(Channel<bool> signal, LeagueType sport, CancellationToken stoppingToken)
    {
        await foreach (var _ in signal.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var transitionService = scope.ServiceProvider.GetRequiredService<ILivePickTransitionService>();
                await transitionService.RecomputeAsync(sport, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LivePickNotificationWatcher: recompute failed for {Sport}", sport);
            }
        }
    }
}
