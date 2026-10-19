using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models.Enum;

namespace FourPlayWebApp.Server.Services;

/// <summary>
/// Routes each sport's scoreboard changes into the one shared
/// ILivePickTransitionService.RecomputeAsync. Subscription, coalescing and the kill switch live
/// in ScoresChangedWatcher.
/// </summary>
public class LivePickNotificationWatcher(
    IEspnCacheService espnCache,
    ICfbCacheService cfbCache,
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<LivePickNotificationWatcher> logger)
    // Kill switch: disable without a deploy if real-world cost turns out worse than expected.
    : ScoresChangedWatcher(espnCache, cfbCache, configuration, "LIVE_PICK_NOTIFICATIONS_ENABLED", logger)
{
    protected override async Task OnScoresChangedAsync(LeagueType sport, ILiveScoresSource source, CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var transitionService = scope.ServiceProvider.GetRequiredService<ILivePickTransitionService>();
        await transitionService.RecomputeAsync(sport, stoppingToken);
    }
}
