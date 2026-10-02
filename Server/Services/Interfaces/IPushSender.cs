using FourPlayWebApp.Server.Models.Data;

namespace FourPlayWebApp.Server.Services.Interfaces;

public record PushPayload(string Title, string Body, string? Url = null);

public interface IPushSender
{
    /// <summary>
    /// Sends one push notification to one subscription. Never throws — a stale subscription
    /// (410/404) is deleted and swallowed, any other failure is logged and swallowed, so a caller
    /// fanning out to many subscriptions never needs its own try/catch per device. Returns true
    /// only if the push service accepted the delivery — false for VAPID-not-configured, a stale
    /// subscription, or any other failure, so a caller (e.g. an admin test-push action) can report
    /// real delivery counts instead of assuming every call succeeded.
    /// </summary>
    Task<bool> SendAsync(PushSubscription subscription, PushPayload payload, CancellationToken cancellationToken = default);
}
