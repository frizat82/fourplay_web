using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models.Data.Dtos;
using Microsoft.AspNetCore.Hosting;

namespace FourPlayWebApp.Server.Services;

public class NotificationDispatcher(
    INotificationPreferencesService preferencesService,
    IPushSubscriptionService subscriptionService,
    IPushSender pushSender,
    ILogger<NotificationDispatcher> logger,
    IWebHostEnvironment environment) : INotificationDispatcher
{
    // Shares DeploymentEnvironment.IsProduction with GoogleEmailSender (see its doc comment for
    // the full incident history behind this exact check) — a real automated trigger must never
    // push to a real user's device from a developer's local run or the deployed dev environment.
    // Deliberately NOT applied to NotificationsController.SendTestPush, which calls IPushSender
    // directly: that's an explicit, admin-initiated, self-targeted debug action meant to validate
    // the pipe in any environment, not a real trigger this gate needs to protect against.
    public async Task DispatchAsync(string userId, Func<NotificationPreferencesDto, bool> isEnabled, PushPayload payload, CancellationToken cancellationToken = default)
    {
        var preferences = await preferencesService.GetAsync(userId);
        if (!isEnabled(preferences)) return;

        var subscriptions = await subscriptionService.GetForUserAsync(userId);
        if (subscriptions.Count == 0) return;

        if (!DeploymentEnvironment.IsProduction(environment))
        {
            logger.LogInformation(
                "🔔 Push suppressed (non-production environment): user {UserId}, {DeviceCount} device(s) — {Title}: {Body}",
                userId, subscriptions.Count, payload.Title, payload.Body);
            return;
        }

        foreach (var subscription in subscriptions)
            await pushSender.SendAsync(subscription, payload, cancellationToken);
    }
}
