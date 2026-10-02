using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models.Data.Dtos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// The single shared call site every real trigger (week-result service, future live-pick watcher)
/// routes through. Mirrors GoogleEmailSender's exact non-production suppression gate — see that
/// class's own doc comment for the full incident history behind the RAILWAY_ENVIRONMENT_NAME +
/// IsDevelopment() combination. Deliberately NOT exercised by NotificationsController.SendTestPush,
/// which calls IPushSender directly (see NotificationsControllerTests).
/// </summary>
[Collection(RailwayEnvironmentNameCollection.Name)]
public class NotificationDispatcherTests : IDisposable
{
    private readonly INotificationPreferencesService _preferencesService = Substitute.For<INotificationPreferencesService>();
    private readonly IPushSubscriptionService _subscriptionService = Substitute.For<IPushSubscriptionService>();
    private readonly IPushSender _pushSender = Substitute.For<IPushSender>();

    public void Dispose() => Environment.SetEnvironmentVariable("RAILWAY_ENVIRONMENT_NAME", null);

    private NotificationDispatcher BuildDispatcher(bool isDevelopment, string? railwayEnvironmentName)
    {
        Environment.SetEnvironmentVariable("RAILWAY_ENVIRONMENT_NAME", railwayEnvironmentName);
        var environment = Substitute.For<IWebHostEnvironment>();
        environment.EnvironmentName.Returns(isDevelopment ? "Development" : "Production");
        return new NotificationDispatcher(_preferencesService, _subscriptionService, _pushSender,
            NullLogger<NotificationDispatcher>.Instance, environment);
    }

    private static List<PushSubscription> OneSubscription() => [new() { Id = 1, UserId = "user-1", Endpoint = "https://push.example/a", P256dh = "p", Auth = "a" }];

    [Fact]
    public async Task DispatchAsync_WhenToggleDisabled_NeverLoadsSubscriptions_NeverSends()
    {
        _preferencesService.GetAsync("user-1").Returns(new NotificationPreferencesDto { NotifyWeekResult = false });

        await BuildDispatcher(isDevelopment: false, railwayEnvironmentName: "production")
            .DispatchAsync("user-1", p => p.NotifyWeekResult, new PushPayload("T", "B"));

        await _subscriptionService.DidNotReceive().GetForUserAsync(Arg.Any<string>());
        await _pushSender.DidNotReceive().SendAsync(Arg.Any<PushSubscription>(), Arg.Any<PushPayload>());
    }

    [Fact]
    public async Task DispatchAsync_WhenToggleEnabled_ButNoSubscribedDevices_DoesNotSend()
    {
        _preferencesService.GetAsync("user-1").Returns(new NotificationPreferencesDto { NotifyWeekResult = true });
        _subscriptionService.GetForUserAsync("user-1").Returns([]);

        await BuildDispatcher(isDevelopment: false, railwayEnvironmentName: "production")
            .DispatchAsync("user-1", p => p.NotifyWeekResult, new PushPayload("T", "B"));

        await _pushSender.DidNotReceive().SendAsync(Arg.Any<PushSubscription>(), Arg.Any<PushPayload>());
    }

    [Fact]
    public async Task DispatchAsync_InProduction_SendsToEveryDevice()
    {
        _preferencesService.GetAsync("user-1").Returns(new NotificationPreferencesDto { NotifyWeekResult = true });
        var subscriptions = OneSubscription();
        _subscriptionService.GetForUserAsync("user-1").Returns(subscriptions);

        await BuildDispatcher(isDevelopment: false, railwayEnvironmentName: "production")
            .DispatchAsync("user-1", p => p.NotifyWeekResult, new PushPayload("T", "B"));

        await _pushSender.Received(1).SendAsync(subscriptions[0], Arg.Any<PushPayload>());
    }

    [Theory]
    [InlineData(false, null)]          // deployed dev environment: IsDevelopment()=false, no Railway var
    [InlineData(false, "development")] // deployed dev environment: explicit Railway var
    [InlineData(true, null)]           // local dev run: IsDevelopment()=true, no Railway var at all
    public async Task DispatchAsync_OutsideProduction_SuppressesSend_ButStillConsumesTheToggleCheck(bool isDevelopment, string? railwayEnvironmentName)
    {
        _preferencesService.GetAsync("user-1").Returns(new NotificationPreferencesDto { NotifyWeekResult = true });
        _subscriptionService.GetForUserAsync("user-1").Returns(OneSubscription());

        await BuildDispatcher(isDevelopment, railwayEnvironmentName)
            .DispatchAsync("user-1", p => p.NotifyWeekResult, new PushPayload("T", "B"));

        await _pushSender.DidNotReceive().SendAsync(Arg.Any<PushSubscription>(), Arg.Any<PushPayload>());
    }
}
