using FourPlayWebApp.Shared.Models.Enum;

namespace FourPlayWebApp.Server.Services.Interfaces;

/// <summary>
/// One implementation for both sports: reacts to a live score change by finding which picks (across
/// EVERY league, not just one) are affected, recomputing their cover state, and notifying on a
/// transition. Called by LivePickNotificationWatcher in reaction to IEspnCacheService's and
/// ICfbCacheService's ScoresChanged events — the only sport-specific wiring in the whole feature.
/// </summary>
public interface ILivePickTransitionService
{
    Task RecomputeAsync(LeagueType sport, CancellationToken cancellationToken = default);
}
