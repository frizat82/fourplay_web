using System.Net;
using System.Text.Json;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Interfaces;
using WebPushSubscription = WebPush.PushSubscription;

namespace FourPlayWebApp.Server.Services;

// Wraps the WebPush NuGet package (VAPID-signed Web Push). Not gated by environment — unlike
// GoogleEmailSender, which blanket-suppresses outside production, every Phase 1 call site is
// either an explicit admin-only "send yourself a test push" debug action, or (from Phase 2/3
// onward) a real trigger whose own dispatcher is responsible for any environment gating. Keeping
// that gate out of this class means it stays a pure, reusable "deliver this payload to this
// subscription" primitive.
public class WebPushSender : IPushSender, IDisposable
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IPushSubscriptionService _subscriptionService;
    private readonly ILogger<WebPushSender> _logger;
    private readonly WebPush.WebPushClient _client;
    private readonly WebPush.VapidDetails? _vapidDetails;

    public WebPushSender(HttpClient httpClient, IPushSubscriptionService subscriptionService, VapidOptions vapidOptions, ILogger<WebPushSender> logger)
    {
        _subscriptionService = subscriptionService;
        _logger = logger;
        _client = new WebPush.WebPushClient(httpClient);

        if (vapidOptions.IsConfigured)
            _vapidDetails = new WebPush.VapidDetails(vapidOptions.Subject!, vapidOptions.PublicKey!, vapidOptions.PrivateKey!);
    }

    public async Task<bool> SendAsync(PushSubscription subscription, PushPayload payload, CancellationToken cancellationToken = default)
    {
        if (_vapidDetails is null)
        {
            _logger.LogError("VAPID_PUBLIC_KEY/VAPID_PRIVATE_KEY/VAPID_SUBJECT are not configured — cannot send push notifications.");
            return false;
        }

        var target = new WebPushSubscription(subscription.Endpoint, subscription.P256dh, subscription.Auth);
        var payloadJson = JsonSerializer.Serialize(payload, PayloadJsonOptions);

        try
        {
            await _client.SendNotificationAsync(target, payloadJson, _vapidDetails, cancellationToken);
            return true;
        }
        catch (WebPush.WebPushException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            // The browser/OS push service itself told us this install no longer exists (uninstalled,
            // permission revoked, or the endpoint rotated) — remove it so future fan-outs stop
            // paying for a send that will only ever fail the same way. Routed through
            // IPushSubscriptionService so this is the only code path that ever deletes a
            // PushSubscriptions row, rather than a second, independently-scoped delete living here.
            _logger.LogInformation("Push subscription for user {UserId} is stale ({StatusCode}) — removing.", subscription.UserId, ex.StatusCode);
            await _subscriptionService.DeleteByEndpointAsync(subscription.Endpoint);
            return false;
        }
        catch (WebPush.WebPushException ex)
        {
            _logger.LogError(ex, "Failed to send push notification to user {UserId}: {StatusCode}", subscription.UserId, ex.StatusCode);
            return false;
        }
    }

    public void Dispose() => _client.Dispose();
}
