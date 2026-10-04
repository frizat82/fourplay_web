using System.Security.Claims;
using FourPlayWebApp.Server.Controllers;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models.Data.Dtos;
using FourPlayWebApp.Shared.Models.Enum;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

public class NotificationsControllerTests
{
    private readonly INotificationPreferencesService _preferencesService = Substitute.For<INotificationPreferencesService>();
    private readonly IPushSubscriptionService _subscriptionService = Substitute.For<IPushSubscriptionService>();
    private readonly IPushSender _pushSender = Substitute.For<IPushSender>();

    private NotificationsController BuildController(string userId = "user-1", VapidOptions? vapidOptions = null)
    {
        var controller = new NotificationsController(_preferencesService, _subscriptionService, _pushSender, vapidOptions ?? new VapidOptions());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId)], "Test")),
            },
        };
        return controller;
    }

    // ── Preferences ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Preferences_AreReadAndSavedForTheRequestedSport()
    {
        var dto = new NotificationPreferencesDto { NotifyWeekResult = true };
        _preferencesService.GetAsync("user-1", LeagueType.Cfb).Returns(dto);
        _preferencesService.UpsertAsync("user-1", LeagueType.Cfb, dto).Returns(dto);

        await BuildController("user-1").GetPreferences(LeagueType.Cfb);
        await BuildController("user-1").PutPreferences(LeagueType.Cfb, dto);

        await _preferencesService.Received(1).GetAsync("user-1", LeagueType.Cfb);
        await _preferencesService.Received(1).UpsertAsync("user-1", LeagueType.Cfb, dto);
    }


    [Fact]
    public async Task GetPreferences_ReturnsTheCallersPreferences()
    {
        var dto = new NotificationPreferencesDto { NotifyWeekResult = true };
        _preferencesService.GetAsync("user-1", LeagueType.Nfl).Returns(dto);

        var result = await BuildController("user-1").GetPreferences(LeagueType.Nfl);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(dto, ok.Value);
    }

    [Fact]
    public async Task PutPreferences_SavesForTheCaller_NeverAForeignUser()
    {
        var request = new NotificationPreferencesDto { NotifyMineCoveringAtFinal = true };
        _preferencesService.UpsertAsync("user-1", LeagueType.Nfl, request).Returns(request);

        var result = await BuildController("user-1").PutPreferences(LeagueType.Nfl, request);

        Assert.IsType<OkObjectResult>(result.Result);
        await _preferencesService.Received(1).UpsertAsync("user-1", LeagueType.Nfl, request);
        // No overload of UpsertAsync accepts a caller-supplied userId from the request body at
        // all — the only userId that can ever reach the service is CurrentUserId.
    }

    // ── VAPID public key ──────────────────────────────────────────────────────

    [Fact]
    public void GetVapidPublicKey_WhenFullyConfigured_ReturnsIt()
    {
        var vapidOptions = new VapidOptions { PublicKey = "test-public-key", PrivateKey = "test-private-key", Subject = "mailto:test@example.com" };

        var result = BuildController(vapidOptions: vapidOptions).GetVapidPublicKey();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<VapidPublicKeyDto>(ok.Value);
        Assert.Equal("test-public-key", dto.PublicKey);
    }

    [Fact]
    public void GetVapidPublicKey_WhenNotConfigured_ReturnsNotFound()
    {
        var result = BuildController(vapidOptions: new VapidOptions()).GetVapidPublicKey();

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public void GetVapidPublicKey_WhenOnlyPublicKeyIsSet_ReturnsNotFound()
    {
        // Config-drift guard: the endpoint must require the SAME "configured" definition
        // WebPushSender uses (all three values), not just the public key alone — otherwise a
        // browser can subscribe using a key the server can never actually sign a send with.
        var vapidOptions = new VapidOptions { PublicKey = "test-public-key" };

        var result = BuildController(vapidOptions: vapidOptions).GetVapidPublicKey();

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ── Subscribe / Unsubscribe ───────────────────────────────────────────────

    [Fact]
    public async Task Subscribe_ValidRequest_CallsServiceForTheCaller()
    {
        var request = new PushSubscriptionRequestDto { Endpoint = "https://push.example/ep1", P256dh = "p", Auth = "a", UserAgent = "ua", Sport = LeagueType.Cfb };

        var result = await BuildController("user-1").Subscribe(request);

        Assert.IsType<NoContentResult>(result);
        await _subscriptionService.Received(1).SubscribeAsync("user-1", "https://push.example/ep1", "p", "a", "ua", LeagueType.Cfb);
    }

    [Theory]
    [InlineData("", "p", "a")]
    [InlineData("https://push.example/ep1", "", "a")]
    [InlineData("https://push.example/ep1", "p", "")]
    public async Task Subscribe_MissingRequiredField_ReturnsBadRequest(string endpoint, string p256dh, string auth)
    {
        var request = new PushSubscriptionRequestDto { Endpoint = endpoint, P256dh = p256dh, Auth = auth };

        var result = await BuildController().Subscribe(request);

        Assert.IsType<BadRequestObjectResult>(result);
        await _subscriptionService.DidNotReceive().SubscribeAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task Unsubscribe_ScopesToTheCaller_NeverAForeignUser()
    {
        var request = new UnsubscribeRequestDto { Endpoint = "https://push.example/ep1" };

        var result = await BuildController("user-1").Unsubscribe(request);

        Assert.IsType<NoContentResult>(result);
        await _subscriptionService.Received(1).UnsubscribeAsync("user-1", "https://push.example/ep1");
    }

    // ── Test push (admin debug endpoint) ──────────────────────────────────────

    private static readonly VapidOptions Configured = new() { PublicKey = "pub", PrivateKey = "priv", Subject = "mailto:a@b.com" };

    [Fact]
    public async Task SendTestPush_WhenVapidNotConfigured_ReturnsDistinctMessage_AndNeverCallsSubscriptionLookup()
    {
        var result = await BuildController("admin-1", vapidOptions: new VapidOptions()).SendTestPush();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<MessageResponseDto>(ok.Value);
        Assert.Contains("not configured", dto.Message, StringComparison.OrdinalIgnoreCase);
        await _subscriptionService.DidNotReceive().GetForUserAsync(Arg.Any<string>());
        await _pushSender.DidNotReceive().SendAsync(Arg.Any<PushSubscription>(), Arg.Any<PushPayload>());
    }

    [Fact]
    public async Task SendTestPush_NoSubscriptions_ReturnsMessage_AndSendsNothing()
    {
        _subscriptionService.GetForUserAsync("admin-1").Returns([]);

        var result = await BuildController("admin-1", vapidOptions: Configured).SendTestPush();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<MessageResponseDto>(ok.Value);
        await _pushSender.DidNotReceive().SendAsync(Arg.Any<PushSubscription>(), Arg.Any<PushPayload>());
    }

    [Fact]
    public async Task SendTestPush_HasSubscriptions_SendsToEachOne_AndReportsTheRealDeliveredCount()
    {
        var subscriptions = new List<PushSubscription>
        {
            new() { Id = 1, UserId = "admin-1", Endpoint = "https://push.example/a", P256dh = "p", Auth = "a" },
            new() { Id = 2, UserId = "admin-1", Endpoint = "https://push.example/b", P256dh = "p", Auth = "a" },
        };
        _subscriptionService.GetForUserAsync("admin-1").Returns(subscriptions);
        // One delivery succeeds, one fails — the reported count must reflect that, not just
        // "every subscription was attempted."
        _pushSender.SendAsync(subscriptions[0], Arg.Any<PushPayload>()).Returns(true);
        _pushSender.SendAsync(subscriptions[1], Arg.Any<PushPayload>()).Returns(false);

        var result = await BuildController("admin-1", vapidOptions: Configured).SendTestPush();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<MessageResponseDto>(ok.Value);
        Assert.Contains("1 of 2", dto.Message);
        await _pushSender.Received(1).SendAsync(subscriptions[0], Arg.Any<PushPayload>());
        await _pushSender.Received(1).SendAsync(subscriptions[1], Arg.Any<PushPayload>());
    }
}
