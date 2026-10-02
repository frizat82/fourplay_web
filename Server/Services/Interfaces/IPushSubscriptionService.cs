using FourPlayWebApp.Server.Models.Data;

namespace FourPlayWebApp.Server.Services.Interfaces;

public interface IPushSubscriptionService
{
    Task<List<PushSubscription>> GetForUserAsync(string userId);

    /// <summary>
    /// Creates or updates (by Endpoint, which is unique per browser install) the caller's
    /// subscription row. Re-subscribing the same device is an upsert, never a duplicate.
    /// </summary>
    Task SubscribeAsync(string userId, string endpoint, string p256dh, string auth, string? userAgent);

    /// <summary>
    /// Deletes the subscription for this endpoint, scoped to the caller — a non-matching or
    /// not-owned endpoint is a silent no-op, matching standard idempotent-delete semantics and
    /// avoiding leaking whether an endpoint belongs to someone else.
    /// </summary>
    Task UnsubscribeAsync(string userId, string endpoint);

    /// <summary>
    /// Removes a subscription by endpoint alone, with no owning-user check — used by
    /// WebPushSender when the push service itself reports (via 404/410) that this specific
    /// endpoint is stale, regardless of which user it belonged to.
    /// </summary>
    Task DeleteByEndpointAsync(string endpoint);
}
