using FourPlayWebApp.Shared.Models.Enum;
using System.Security.Claims;
using FourPlayWebApp.Server.Auth;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models.Data.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FourPlayWebApp.Server.Controllers;

[Authorize]
[ApiController]
[Route("api/notifications")]
public class NotificationsController(
    INotificationPreferencesService preferencesService,
    IPushSubscriptionService subscriptionService,
    IPushSender pushSender,
    VapidOptions vapidOptions) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet("preferences")]
    public async Task<ActionResult<NotificationPreferencesDto>> GetPreferences([FromQuery] LeagueType sport = LeagueType.Nfl)
    {
        return Ok(await preferencesService.GetAsync(CurrentUserId, sport));
    }

    [HttpPut("preferences")]
    public async Task<ActionResult<NotificationPreferencesDto>> PutPreferences([FromQuery] LeagueType sport, [FromBody] NotificationPreferencesDto preferences)
    {
        return Ok(await preferencesService.UpsertAsync(CurrentUserId, sport, preferences));
    }

    [HttpGet("vapid-public-key")]
    public ActionResult<VapidPublicKeyDto> GetVapidPublicKey()
    {
        // Checks the same IsConfigured (public+private+subject) that WebPushSender requires to
        // actually send — handing out a public key the server can't yet sign against would let a
        // browser subscribe into a dead end.
        if (!vapidOptions.IsConfigured)
            return NotFound();
        return Ok(new VapidPublicKeyDto { PublicKey = vapidOptions.PublicKey! });
    }

    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] PushSubscriptionRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Endpoint) || string.IsNullOrWhiteSpace(request.P256dh) || string.IsNullOrWhiteSpace(request.Auth))
            return BadRequest("Endpoint, P256dh, and Auth are required.");

        await subscriptionService.SubscribeAsync(CurrentUserId, request.Endpoint, request.P256dh, request.Auth, request.UserAgent, request.Sport);
        return NoContent();
    }

    [HttpDelete("subscribe")]
    public async Task<IActionResult> Unsubscribe([FromBody] UnsubscribeRequestDto request)
    {
        await subscriptionService.UnsubscribeAsync(CurrentUserId, request.Endpoint);
        return NoContent();
    }

    // Admin-only: validates the full subscribe -> store -> VAPID-sign -> deliver pipeline against
    // the admin's own subscribed device(s), before any real trigger (Phase 2/3) exists to exercise it.
    [HttpPost("test-push")]
    [Authorize(Roles = AppRoles.Administrator)]
    public async Task<ActionResult<MessageResponseDto>> SendTestPush()
    {
        if (!vapidOptions.IsConfigured)
            return Ok(new MessageResponseDto("VAPID is not configured on this environment — nothing was sent."));

        var subscriptions = await subscriptionService.GetForUserAsync(CurrentUserId);
        if (subscriptions.Count == 0)
            return Ok(new MessageResponseDto("No subscribed devices for this account."));

        var payload = new PushPayload("IV League", "Test push notification ✅");
        var sentCount = 0;
        foreach (var subscription in subscriptions)
        {
            if (await pushSender.SendAsync(subscription, payload))
                sentCount++;
        }

        return Ok(new MessageResponseDto($"Sent to {sentCount} of {subscriptions.Count} device(s)."));
    }
}
