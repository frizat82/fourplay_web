using FourPlayWebApp.Shared.Models.Data.Dtos;

namespace FourPlayWebApp.Server.Services.Interfaces;

public interface INotificationDispatcher
{
    /// <summary>
    /// The single call site every real trigger (week-result job, future live-pick watcher) routes
    /// through: loads the recipient's preferences and checks the one relevant toggle via
    /// <paramref name="isEnabled"/>, loads their subscribed devices, and sends to each — all as a
    /// no-op if the toggle is off or no device is subscribed. Non-production environments log a
    /// suppressed line instead of sending (mirrors GoogleEmailSender's exact gate).
    /// </summary>
    Task DispatchAsync(string userId, Func<NotificationPreferencesDto, bool> isEnabled, PushPayload payload, CancellationToken cancellationToken = default);
}
